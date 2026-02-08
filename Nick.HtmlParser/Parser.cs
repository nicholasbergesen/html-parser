namespace HtmlParser
{
    public class Parser
    {
        private static readonly HashSet<NodeType> _voidTags = new()
        {
            NodeType.area,
            NodeType.@base,
            NodeType.br,
            NodeType.col,
            NodeType.command,
            NodeType.embed,
            NodeType.hr,
            NodeType.img,
            NodeType.input,
            NodeType.keygen,
            NodeType.link,
            NodeType.meta,
            NodeType.param,
            NodeType.source,
            NodeType.track,
            NodeType.wbr
        };

        private static readonly HashSet<NodeType> _skipTag = new()
        {
            NodeType.script, //valid '<' characters inside script can cause parse errors.
            NodeType.style
        };

        public static IReadOnlyList<INode> Parse(string html, bool loadContent = false)
        {
            List<Node> nodes = new();
            int pos = 0;
            int depth = 0;
            while (pos < html.Length)
            {
                //go to next open chevron
                if (html[pos] != '<')
                {
                    pos++;
                    continue;
                }

                //skip if '<' is at the very end of the document
                if (pos + 1 >= html.Length)
                {
                    pos++;
                    continue;
                }

                //skip over comments in the document
                if (html[pos + 1] == '!')
                {
                    bool isDoctype = pos + 9 <= html.Length
                        && html[(pos + 2)..(pos + 9)].Equals("DOCTYPE", StringComparison.InvariantCultureIgnoreCase);

                    if (isDoctype)
                    {
                        pos += 9;//skip ahead doctype chars.
                        while (++pos < html.Length && html[pos] != '>') ;
                        pos++;
                    }
                    else //if not doctype then assume is comment
                    {
                        while (pos + 2 < html.Length && !(html[pos] == '-' && html[pos + 1] == '-' && html[pos + 2] == '>'))
                        {
                            pos++;
                        }
                        pos += 3;
                    }
                    continue;
                }
                else if (html[pos + 1] == '?')
                {
                    while (pos + 1 < html.Length && !(html[++pos] == '?' && html[pos + 1] == '>')) ;

                    pos += 2;
                    continue;
                }

                bool isCloseTag = html[pos + 1] == '/';
                var closeChevronPos = pos;
                while (++closeChevronPos < html.Length && html[closeChevronPos] != '>')
                {
                    char c = html[closeChevronPos];

                    //ignore chevrons found in in quotes
                    if (c == '\'')
                    {
                        while (html[++closeChevronPos] != '\'')
                        {
                            //handle malformed quote chars
                            //Exit if there is an opening tag preceeded by whitespace and then a closing tag (in reverse)
                            if (html[closeChevronPos] == '<')
                            {
                                int end = closeChevronPos;
                                while (html[--end] != '>') ;
                                if (string.IsNullOrWhiteSpace(html[(end + 1)..closeChevronPos]))
                                {
                                    closeChevronPos = end - 1; //-1 added for outer while loop increment
                                    break;
                                }
                            }
                        }
                    }
                    if (c == '\"')
                    {
                        while (html[++closeChevronPos] != '\"')
                        {
                            //handle malformed quote chars
                            //Exit if there is an opening tag preceeded by whitespace and then a closing tag (in reverse)
                            if (html[closeChevronPos] == '<')
                            {
                                int end = closeChevronPos;
                                while (html[--end] != '>') ;
                                if (string.IsNullOrWhiteSpace(html[(end + 1)..closeChevronPos]))
                                {
                                    closeChevronPos = end - 1; //-1 added for outer while loop increment
                                    break;
                                }
                            }
                        }
                    }
                }
                //if we ran past the end of the document, stop parsing
                if (closeChevronPos >= html.Length)
                    break;

                var isSelfClosing = html[closeChevronPos - 1] == '/';

                var tagNameStartPos = isCloseTag ? pos + 2 : pos + 1;
                var tagName = html[tagNameStartPos..(isSelfClosing ? closeChevronPos - 1 : closeChevronPos)];
                var node = new Node(tagName, depth, pos);
                var isSkipTag = _skipTag.Contains(node.Type);

                if (isSelfClosing || _voidTags.Contains(node.Type))
                {

                    node.SelfCloseNode(content: loadContent ? html[node.OpenPosition..(closeChevronPos + 1)] : null);
                    nodes.Add(node);
                }
                else if (isSkipTag)
                {
                    var openTag = $"<{node.Name}>";
                    var closeTag = $"</{node.Name}>";
                    int closeTagCounter = 0;
                    int closeTagPos = closeChevronPos;
                    //this loop caters for nested skip tags. e.g <script><script></script></script>
                    while (closeTagCounter < 1)
                    {
                        closeTagPos++;

                        if (closeTagPos > (html.Length - closeTag.Length))
                            throw new Exception($"Unable to find close tag for {node.Name} at char position {pos}");

                        if (html.AsSpan(closeTagPos, openTag.Length).Equals(openTag, StringComparison.OrdinalIgnoreCase))
                            closeTagCounter--;
                        else if (html.AsSpan(closeTagPos, closeTag.Length).Equals(closeTag, StringComparison.OrdinalIgnoreCase))
                            closeTagCounter++;
                    }

                    node.CloseNode(closePosition: closeTagPos + closeTag.Length, content: loadContent ? html[node.OpenPosition..(closeTagPos + closeTag.Length)] : null);
                    nodes.Add(node);
                    pos = closeTagPos;
                }
                else if (isCloseTag)
                {
                    depth--;
                    var unclosedParentNode = nodes.FirstOrDefault(x =>
                        x.Name == node.Name
                        && x.Depth == depth
                        && x.ClosedPosition == -1);

                    //if `unclosedParentNode` is null its possible there are unclosed tags causing depth calculation to be incorrect.
                    //Solution: Check for unclosed tags in previous depth, and close them as self closed tags.
                    //depth-- for each unclosed tag. All tags after the unclosed tag up to the current position will need their depth value corrected.
                    //Note: Could be multiple unclosed tags.
                    if (unclosedParentNode is null)
                    {
                        var matchingUnclosedNode = nodes
                            .OrderByDescending(x => x.OpenPosition)
                            .FirstOrDefault(x =>
                                x.Name == node.Name &&
                                x.ClosedPosition == -1);

                        //if no matching open tag found, then ignore rogue closing tag.
                        //TODO: log as document error.
                        if (matchingUnclosedNode is null)
                        {
                            pos++;
                            depth++;//restore depth
                            continue;
                        }

                        var unclosedChildren = nodes
                            .Where(x => matchingUnclosedNode.Depth < x.Depth
                                && matchingUnclosedNode.OpenPosition < x.OpenPosition
                                && x.ClosedPosition == -1);

                        var closedChildren = nodes
                            .Where(x => matchingUnclosedNode.Depth < x.Depth
                                && matchingUnclosedNode.OpenPosition < x.OpenPosition
                                && x.ClosedPosition < pos)
                            .ToList(); //call .ToList() to execute .Where before populating self closing in unclosedChildren enumerable.

                        //close all unclosed children
                        int depthCorrection = 0;
                        foreach (var child in unclosedChildren)
                        {
                            child.SelfCloseNode();
                            depthCorrection++;
                        }

                        //correct child depths by correction amount
                        depth -= depthCorrection;
                        foreach (var child in closedChildren)
                        {
                            child.Depth -= depthCorrection;
                        }

                        //attempt to refetch unclosed tag with updated depth
                        unclosedParentNode = nodes.FirstOrDefault(x =>
                            x.Name == node.Name
                            && x.Depth == depth
                            && x.ClosedPosition == -1);

                        if (unclosedParentNode is null)
                            throw new Exception($"Unable to parse document. Error occored parsing character at position {pos}. Possible issue with {matchingUnclosedNode.Name} at character position {matchingUnclosedNode.OpenPosition}");

                    }

                    var childNodes = nodes.Where(x =>
                            x.Depth == depth + 1
                            && unclosedParentNode.OpenPosition < x.OpenPosition
                            && x.ClosedPosition < closeChevronPos)
                        .ToList();

                    foreach (var child in childNodes)
                    {
                        child.Parent = unclosedParentNode;
                    }

                    unclosedParentNode.CloseNode(closePosition: closeChevronPos, children: childNodes, content: loadContent ? html[unclosedParentNode.OpenPosition..(closeChevronPos + 1)] : null);
                }
                else
                {
                    nodes.Add(node);
                    depth++;
                }

                pos++;
            }

            return nodes;
        }
    }
}
