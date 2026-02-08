using HtmlParser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Test
{
    [TestClass]
    public class TestEdgeCases
    {
        // --- Bounds / crash tests ---

        [TestMethod]
        public void Parse_LoneOpenChevronAtEnd_DoesNotThrow()
        {
            var nodes = Parser.Parse("<");
            Assert.AreEqual(0, nodes.Count);
        }

        [TestMethod]
        public void Parse_UnterminatedComment_DoesNotThrow()
        {
            var nodes = Parser.Parse("<div><!-- unterminated comment");
            // Should not throw; the div is unclosed but parser should not crash
            Assert.IsTrue(nodes.Count >= 1);
        }

        [TestMethod]
        public void Parse_UnterminatedDoctype_DoesNotThrow()
        {
            var nodes = Parser.Parse("<!DOCTYPE html");
            Assert.AreEqual(0, nodes.Count);
        }

        [TestMethod]
        public void Parse_ShortBangTag_DoesNotThrow()
        {
            // "<!X>" — the '!' branch with fewer than 7 chars remaining for DOCTYPE check
            var nodes = Parser.Parse("<!X>");
            Assert.AreEqual(0, nodes.Count);
        }

        [TestMethod]
        public void Parse_UnterminatedXmlPI_DoesNotThrow()
        {
            var nodes = Parser.Parse("<?xml version=\"1.0\"");
            Assert.AreEqual(0, nodes.Count);
        }

        [TestMethod]
        public void Parse_UnclosedTag_DoesNotThrow()
        {
            var nodes = Parser.Parse("<div class=\"test\"");
            // The tag never closes with '>'; parser should not crash
            Assert.AreEqual(0, nodes.Count);
        }

        // --- DOCTYPE ---

        [TestMethod]
        public void Parse_DoctypeIsSkipped()
        {
            var nodes = Parser.Parse("<!DOCTYPE html><div></div>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("div", nodes[0].Name);
        }

        [TestMethod]
        public void Parse_DoctypeWithAttributes_IsSkipped()
        {
            var nodes = Parser.Parse("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\"><div></div>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("div", nodes[0].Name);
        }

        [TestMethod]
        public void Parse_DoctypeLowercase_IsSkipped()
        {
            var nodes = Parser.Parse("<!doctype html><p></p>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("p", nodes[0].Name);
        }

        // --- Comment handling ---

        [TestMethod]
        public void Parse_CommentsAreSkipped()
        {
            var nodes = Parser.Parse("<!-- comment --><p></p>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("p", nodes[0].Name);
        }

        [TestMethod]
        public void Parse_MultipleComments_AreSkipped()
        {
            var nodes = Parser.Parse("<!-- first --><div></div><!-- second --><p></p><!-- third -->");
            Assert.AreEqual(2, nodes.Count);
            Assert.AreEqual("div", nodes[0].Name);
            Assert.AreEqual("p", nodes[1].Name);
        }

        [TestMethod]
        public void Parse_CommentBetweenTags()
        {
            var nodes = Parser.Parse("<div><!-- inline comment --><span></span></div>");
            Assert.AreEqual(2, nodes.Count);
            Assert.AreEqual("div", nodes[0].Name);
            Assert.AreEqual("span", nodes[1].Name);
        }

        // --- XML processing instruction ---

        [TestMethod]
        public void Parse_XmlPIIsSkipped()
        {
            var nodes = Parser.Parse("<?xml version=\"1.0\"?><div></div>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("div", nodes[0].Name);
        }

        // --- Void / self-closing tags ---

        [TestMethod]
        public void Parse_VoidTagsAreSelfClosed()
        {
            var nodes = Parser.Parse("<br><hr><img src=\"test.png\">");
            Assert.AreEqual(3, nodes.Count);
            Assert.IsTrue(nodes.All(n => n.ClosedPosition > 0));
        }

        [TestMethod]
        public void Parse_ExplicitSelfClosingTag()
        {
            var nodes = Parser.Parse("<input type=\"text\" />");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("input", nodes[0].Name);
        }

        [TestMethod]
        public void Parse_AllVoidTagTypes_AreSelfClosed()
        {
            var html = "<area><base><br><col><command><embed><hr><img><input><keygen><link><meta><param><source><track><wbr>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(16, nodes.Count);
            Assert.IsTrue(nodes.All(n => n.ClosedPosition >= 0), "All void tags should be self-closed");
        }

        [TestMethod]
        public void Parse_VoidTagInsideDiv_CorrectDepth()
        {
            var nodes = Parser.Parse("<div><br><hr></div>");
            Assert.AreEqual(3, nodes.Count);
            Assert.AreEqual(0, nodes[0].Depth); // div
            Assert.AreEqual(1, nodes[1].Depth); // br
            Assert.AreEqual(1, nodes[2].Depth); // hr
        }

        [TestMethod]
        public void Parse_VoidTag_LoadContent()
        {
            var nodes = Parser.Parse("<img src=\"a.png\">", loadContent: true);
            Assert.AreEqual(1, nodes.Count);
            Assert.IsNotNull(nodes[0].Content);
            Assert.IsTrue(nodes[0].Content.Contains("img"));
        }

        [TestMethod]
        public void Parse_MetaTag_SelfClosed()
        {
            var nodes = Parser.Parse("<meta charset=\"utf-8\">");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("meta", nodes[0].Name);
            Assert.AreEqual("utf-8", nodes[0].Attributes["charset"]);
            Assert.IsTrue(nodes[0].ClosedPosition >= 0);
        }

        [TestMethod]
        public void Parse_SelfClosingNonVoidTag()
        {
            // e.g. <div /> — explicit self-close on a non-void tag
            var nodes = Parser.Parse("<div />");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("div", nodes[0].Name);
            Assert.IsTrue(nodes[0].ClosedPosition >= 0);
        }

        // --- Script / style skip tags ---

        [TestMethod]
        public void Parse_ScriptTagContentIsSkipped()
        {
            var html = "<script>var x = '<div>';</script><p></p>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(2, nodes.Count);
            Assert.AreEqual("script", nodes[0].Name);
            Assert.AreEqual("p", nodes[1].Name);
        }

        [TestMethod]
        public void Parse_StyleTagContentIsSkipped()
        {
            var html = "<style>div > p { color: red; }</style><span></span>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(2, nodes.Count);
            Assert.AreEqual("style", nodes[0].Name);
            Assert.AreEqual("span", nodes[1].Name);
        }

        [TestMethod]
        public void Parse_NestedScriptTags()
        {
            var html = "<script><script></script></script><div></div>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(2, nodes.Count);
            Assert.AreEqual("script", nodes[0].Name);
            Assert.AreEqual("div", nodes[1].Name);
        }

        [TestMethod]
        public void Parse_ScriptTag_CaseInsensitive()
        {
            var html = "<script>var x = 1;</SCRIPT><p></p>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(2, nodes.Count);
            Assert.AreEqual("script", nodes[0].Name);
            Assert.AreEqual("p", nodes[1].Name);
        }

        [TestMethod]
        public void Parse_ScriptTagWithAttributes()
        {
            var html = "<script type=\"text/javascript\">var x=1;</script><p></p>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(2, nodes.Count);
            Assert.AreEqual("script", nodes[0].Name);
            Assert.AreEqual("p", nodes[1].Name);
        }

        [TestMethod]
        public void Parse_StyleTag_LoadContent()
        {
            var html = "<style>body { margin: 0; }</style>";
            var nodes = Parser.Parse(html, loadContent: true);
            Assert.AreEqual(1, nodes.Count);
            Assert.IsNotNull(nodes[0].Content);
            Assert.IsTrue(nodes[0].Content.Contains("margin: 0"));
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void Parse_ScriptTag_MissingCloseTag_Throws()
        {
            Parser.Parse("<script>var x = 1;");
        }

        // --- Depth / parent-child relationships ---

        [TestMethod]
        public void Parse_NestedTags_CorrectDepth()
        {
            var nodes = Parser.Parse("<div><p><span></span></p></div>");
            Assert.AreEqual(3, nodes.Count);
            Assert.AreEqual(0, nodes[0].Depth); // div
            Assert.AreEqual(1, nodes[1].Depth); // p
            Assert.AreEqual(2, nodes[2].Depth); // span
        }

        [TestMethod]
        public void Parse_ParentChildRelationships()
        {
            var nodes = Parser.Parse("<div><p></p><span></span></div>");
            Assert.AreEqual(3, nodes.Count);
            var div = nodes[0];
            var p = nodes[1];
            var span = nodes[2];
            Assert.IsNotNull(div.Children);
            Assert.AreEqual(2, div.Children.Count);
            Assert.AreEqual(div, p.Parent);
            Assert.AreEqual(div, span.Parent);
        }

        [TestMethod]
        public void Parse_DeepNesting_ChildrenAndParents()
        {
            var html = "<div><ul><li><a></a></li></ul></div>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(4, nodes.Count);
            Assert.AreEqual(0, nodes[0].Depth); // div
            Assert.AreEqual(1, nodes[1].Depth); // ul
            Assert.AreEqual(2, nodes[2].Depth); // li
            Assert.AreEqual(3, nodes[3].Depth); // a

            // parent chain
            Assert.AreEqual(nodes[2], nodes[3].Parent); // a -> li
            Assert.AreEqual(nodes[1], nodes[2].Parent); // li -> ul
            Assert.AreEqual(nodes[0], nodes[1].Parent); // ul -> div

            // children
            Assert.IsNotNull(nodes[0].Children);
            Assert.AreEqual(1, nodes[0].Children.Count); // div has ul
            Assert.IsNotNull(nodes[2].Children);
            Assert.AreEqual(1, nodes[2].Children.Count); // li has a
        }

        [TestMethod]
        public void Parse_DeepNesting_LoadContent()
        {
            var html = "<div><p><span>text</span></p></div>";
            var nodes = Parser.Parse(html, loadContent: true);
            var div = nodes.First(n => n.Name == "div");
            var p = nodes.First(n => n.Name == "p");
            var span = nodes.First(n => n.Name == "span");
            Assert.IsNotNull(div.Content);
            Assert.IsTrue(div.Content.Contains("<p><span>text</span></p>"));
            Assert.IsNotNull(p.Content);
            Assert.IsTrue(p.Content.Contains("<span>text</span>"));
            Assert.IsNotNull(span.Content);
            Assert.IsTrue(span.Content.Contains("text"));
        }

        [TestMethod]
        public void Parse_ClosedTag_NoChildren_HasEmptyChildrenList()
        {
            var nodes = Parser.Parse("<div></div>");
            Assert.AreEqual(1, nodes.Count);
            Assert.IsNotNull(nodes[0].Children);
            Assert.AreEqual(0, nodes[0].Children.Count);
        }

        [TestMethod]
        public void Parse_VoidTagChildren_AreNotSetAsParent()
        {
            // void tags are self-closed, they don't get children
            var nodes = Parser.Parse("<div><br></div>");
            var br = nodes.First(n => n.Name == "br");
            Assert.IsNull(br.Children);
        }

        // --- Content loading ---

        [TestMethod]
        public void Parse_LoadContentTrue_PopulatesContent()
        {
            var html = "<div><p>Hello</p></div>";
            var nodes = Parser.Parse(html, loadContent: true);
            var div = nodes.First(n => n.Name == "div");
            Assert.IsNotNull(div.Content);
            Assert.IsTrue(div.Content.Contains("<p>Hello</p>"));
        }

        [TestMethod]
        public void Parse_LoadContentFalse_ContentIsNull()
        {
            var html = "<div><p>Hello</p></div>";
            var nodes = Parser.Parse(html, loadContent: false);
            Assert.IsTrue(nodes.All(n => n.Content == null));
        }

        [TestMethod]
        public void Parse_LoadContent_SelfClosingVoidTag()
        {
            var nodes = Parser.Parse("<br />", loadContent: true);
            Assert.AreEqual(1, nodes.Count);
            Assert.IsNotNull(nodes[0].Content);
        }

        // --- Attribute parsing edge cases ---

        [TestMethod]
        public void Node_NoAttributes()
        {
            var nodes = Parser.Parse("<div></div>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual(0, nodes[0].Attributes.Count);
        }

        [TestMethod]
        public void Node_MixedQuoteAttributes()
        {
            var nodes = Parser.Parse("<div class=\"foo\" data-val='bar'></div>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("foo", nodes[0].Attributes["class"]);
            Assert.AreEqual("bar", nodes[0].Attributes["data-val"]);
        }

        [TestMethod]
        public void Node_UnknownTagType()
        {
            var nodes = Parser.Parse("<customtag></customtag>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual(NodeType.unknown, nodes[0].Type);
            Assert.AreEqual("customtag", nodes[0].Name);
        }

        [TestMethod]
        public void Node_DuplicateAttributes_FirstWins()
        {
            // TryAdd keeps the first value; the duplicate is ignored
            var node = new Node("div class=\"first\" class=\"second\"", 0, 0);
            Assert.AreEqual(1, node.Attributes.Count);
            Assert.AreEqual("first", node.Attributes["class"]);
        }

        [TestMethod]
        public void Node_UnmatchedClosingQuote_GracefullyStops()
        {
            // Only an opening quote with no matching close — attribute parsing should break gracefully
            var node = new Node("div data-val=\"unterminated", 0, 0);
            Assert.AreEqual("div", node.Name);
            // Should not throw; attributes may be empty or partial
            Assert.IsNotNull(node.Attributes);
        }

        [TestMethod]
        public void Node_EmptyAttributeValue()
        {
            var node = new Node("div class=\"\"", 0, 0);
            Assert.AreEqual(1, node.Attributes.Count);
            Assert.AreEqual("", node.Attributes["class"]);
        }

        [TestMethod]
        public void Node_AttributeValueContainsSpaces()
        {
            var node = new Node("div class=\"foo bar baz\"", 0, 0);
            Assert.AreEqual("foo bar baz", node.Attributes["class"]);
        }

        [TestMethod]
        public void Node_MultipleAttributes_SingleQuotes()
        {
            var node = new Node("div data-a='x' data-b='y'", 0, 0);
            Assert.AreEqual(2, node.Attributes.Count);
            Assert.AreEqual("x", node.Attributes["data-a"]);
            Assert.AreEqual("y", node.Attributes["data-b"]);
        }

        // --- Attribute values containing chevrons (quoted chevron paths) ---

        [TestMethod]
        public void Parse_DoubleQuotedAttributeContainingChevron()
        {
            // The '>' inside double quotes should not end the tag
            var nodes = Parser.Parse("<div data-val=\"a>b\"></div>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("div", nodes[0].Name);
        }

        [TestMethod]
        public void Parse_SingleQuotedAttributeContainingChevron()
        {
            // The '>' inside single quotes should not end the tag
            var nodes = Parser.Parse("<div data-val='a>b'></div>");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("div", nodes[0].Name);
        }

        // --- Node.ToString ---

        [TestMethod]
        public void Node_ToString_Format()
        {
            var node = new Node("div", 0, 5);
            Assert.AreEqual("div 5 -1", node.ToString());

            node.SelfCloseNode();
            Assert.AreEqual($"div 5 {5 + 3}", node.ToString()); // "div" length = 3
        }

        // --- Node.SelfCloseNode ---

        [TestMethod]
        public void Node_SelfCloseNode_SetsClosedPosition()
        {
            var node = new Node("img src=\"x\"", 0, 10);
            Assert.AreEqual(-1, node.ClosedPosition);
            node.SelfCloseNode();
            Assert.AreEqual(10 + "img src=\"x\"".Length, node.ClosedPosition);
        }

        [TestMethod]
        public void Node_SelfCloseNode_WithContent()
        {
            var node = new Node("br", 0, 0);
            node.SelfCloseNode(content: "<br>");
            Assert.AreEqual("<br>", node.Content);
        }

        // --- Node.CloseNode ---

        [TestMethod]
        public void Node_CloseNode_SetsPositionAndChildren()
        {
            var node = new Node("div", 0, 0);
            var childNode = new Node("p", 1, 5);
            childNode.CloseNode(closePosition: 15);

            node.CloseNode(closePosition: 20, children: new[] { (INode)childNode });
            Assert.AreEqual(20, node.ClosedPosition);
            Assert.IsNotNull(node.Children);
            Assert.AreEqual(1, node.Children.Count);
        }

        // --- Malformed HTML recovery ---

        [TestMethod]
        public void Parse_RogueClosingTag_IsIgnored()
        {
            // </span> has no matching open tag — should be skipped
            var nodes = Parser.Parse("<div></span></div>");
            var div = nodes.FirstOrDefault(n => n.Name == "div");
            Assert.IsNotNull(div);
            Assert.IsTrue(div.ClosedPosition > 0);
        }

        [TestMethod]
        public void Parse_MissingClosingTag_RecoveryWorks()
        {
            // <b> is never closed; parser should self-close it during recovery
            var nodes = Parser.Parse("<div><b><p></p></div>");
            Assert.IsTrue(nodes.Count >= 3);
            var div = nodes.First(n => n.Name == "div");
            Assert.IsTrue(div.ClosedPosition > 0);
        }

        [TestMethod]
        public void Parse_MultipleUnclosedTags_DepthCorrected()
        {
            // Both <b> and <i> are unclosed inside <div>; recovery should self-close them and correct depth
            var nodes = Parser.Parse("<div><b><i><p></p></div>");
            var div = nodes.First(n => n.Name == "div");
            Assert.IsTrue(div.ClosedPosition > 0);
            var b = nodes.First(n => n.Name == "b");
            var i = nodes.First(n => n.Name == "i");
            // b and i should be self-closed during recovery
            Assert.IsTrue(b.ClosedPosition >= 0);
            Assert.IsTrue(i.ClosedPosition >= 0);
        }

        [TestMethod]
        public void Parse_MultipleRogueClosingTags_AreIgnored()
        {
            var nodes = Parser.Parse("<div></em></strong></div>");
            var div = nodes.First(n => n.Name == "div");
            Assert.IsTrue(div.ClosedPosition > 0);
        }

        [TestMethod]
        public void Parse_UnclosedTag_ClosedChildrenDepthCorrected()
        {
            // <b> is unclosed; <em> is properly closed inside — its depth should be corrected
            var html = "<div><b><em></em><p></p></div>";
            var nodes = Parser.Parse(html);
            var div = nodes.First(n => n.Name == "div");
            Assert.IsTrue(div.ClosedPosition > 0);
            var em = nodes.First(n => n.Name == "em");
            var p = nodes.First(n => n.Name == "p");
            // After correction, em and p should be at depth 1 (children of div)
            Assert.AreEqual(div.Depth + 1, em.Depth);
            Assert.AreEqual(div.Depth + 1, p.Depth);
        }

        // --- Empty document ---

        [TestMethod]
        public void Parse_EmptyString_ReturnsEmpty()
        {
            var nodes = Parser.Parse("");
            Assert.AreEqual(0, nodes.Count);
        }

        [TestMethod]
        public void Parse_PlainText_ReturnsEmpty()
        {
            var nodes = Parser.Parse("Hello World, no tags here.");
            Assert.AreEqual(0, nodes.Count);
        }

        [TestMethod]
        public void Parse_WhitespaceOnly_ReturnsEmpty()
        {
            var nodes = Parser.Parse("   \n\t\r\n   ");
            Assert.AreEqual(0, nodes.Count);
        }

        // --- Self-closing with attributes ---

        [TestMethod]
        public void Parse_SelfClosingWithAttributes()
        {
            var nodes = Parser.Parse("<img src=\"photo.jpg\" alt=\"A photo\" />");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("img", nodes[0].Name);
            Assert.AreEqual("photo.jpg", nodes[0].Attributes["src"]);
            Assert.AreEqual("A photo", nodes[0].Attributes["alt"]);
        }

        // --- Sibling tags ---

        [TestMethod]
        public void Parse_SiblingTags_SameDepth()
        {
            var nodes = Parser.Parse("<p>A</p><p>B</p><p>C</p>");
            Assert.AreEqual(3, nodes.Count);
            Assert.IsTrue(nodes.All(n => n.Depth == 0));
        }

        // --- Script tag with loadContent ---

        [TestMethod]
        public void Parse_ScriptTag_LoadContent()
        {
            var html = "<script>alert('hi');</script>";
            var nodes = Parser.Parse(html, loadContent: true);
            Assert.AreEqual(1, nodes.Count);
            Assert.IsNotNull(nodes[0].Content);
            Assert.IsTrue(nodes[0].Content.Contains("alert('hi');"));
        }

        // --- Open positions ---

        [TestMethod]
        public void Parse_OpenPosition_IsCorrect()
        {
            var html = "<div><p></p></div>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(0, nodes[0].OpenPosition);  // <div> starts at 0
            Assert.AreEqual(5, nodes[1].OpenPosition);  // <p> starts at 5
        }

        [TestMethod]
        public void Parse_ClosedPosition_PointsToCloseChevron()
        {
            var html = "<div></div>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(1, nodes.Count);
            // Close position should point to the close tag's '>'
            Assert.AreEqual(html.IndexOf("</div>") + "</div>".Length - 1, nodes[0].ClosedPosition);
        }

        // --- Mixed content ---

        [TestMethod]
        public void Parse_TextBetweenTags_IsIgnored()
        {
            var nodes = Parser.Parse("Hello <b>world</b> goodbye");
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual("b", nodes[0].Name);
        }

        [TestMethod]
        public void Parse_MixedVoidAndNonVoidTags()
        {
            var html = "<div><p>Text</p><br><span>More</span><hr></div>";
            var nodes = Parser.Parse(html);
            Assert.AreEqual(5, nodes.Count);
            Assert.AreEqual("div", nodes[0].Name);
            Assert.AreEqual("p", nodes[1].Name);
            Assert.AreEqual("br", nodes[2].Name);
            Assert.AreEqual("span", nodes[3].Name);
            Assert.AreEqual("hr", nodes[4].Name);
        }

        // --- Full HTML document structure ---

        [TestMethod]
        public void Parse_MinimalHtmlDocument()
        {
            var html = "<!DOCTYPE html><html><head><title></title><meta charset=\"utf-8\"></head><body><div></div></body></html>";
            var nodes = Parser.Parse(html);

            Assert.AreEqual("html", nodes[0].Name);
            Assert.AreEqual(0, nodes[0].Depth);

            var head = nodes.First(n => n.Name == "head");
            Assert.AreEqual(1, head.Depth);

            var meta = nodes.First(n => n.Name == "meta");
            Assert.AreEqual(2, meta.Depth);
            Assert.IsTrue(meta.ClosedPosition >= 0); // void tag

            var body = nodes.First(n => n.Name == "body");
            Assert.AreEqual(1, body.Depth);
        }

        // --- Case sensitivity for tag names ---

        [TestMethod]
        public void Parse_UppercaseTagNames_RecognizedAsKnownType()
        {
            // Node constructor uses ignoreCase:true for Enum.TryParse
            var node = new Node("DIV", 0, 0);
            Assert.AreEqual(NodeType.div, node.Type);
            Assert.AreEqual("DIV", node.Name);
        }

        [TestMethod]
        public void Parse_MixedCaseTagName()
        {
            var node = new Node("Span", 0, 0);
            Assert.AreEqual(NodeType.span, node.Type);
        }

        // --- Tags immediately adjacent ---

        [TestMethod]
        public void Parse_AdjacentSelfClosingTags()
        {
            var nodes = Parser.Parse("<br><br><br>");
            Assert.AreEqual(3, nodes.Count);
            Assert.IsTrue(nodes.All(n => n.Name == "br"));
        }

        [TestMethod]
        public void Parse_AdjacentOpenCloseTags()
        {
            var nodes = Parser.Parse("<a></a><b></b><i></i>");
            Assert.AreEqual(3, nodes.Count);
            Assert.IsTrue(nodes.All(n => n.ClosedPosition > 0));
            Assert.IsTrue(nodes.All(n => n.Depth == 0));
        }

        // --- Unclosed tags at end of document ---

        [TestMethod]
        public void Parse_UnclosedTagAtEnd_StillReturned()
        {
            var nodes = Parser.Parse("<div><p>");
            // Both tags are opened but never closed
            Assert.AreEqual(2, nodes.Count);
            Assert.AreEqual(-1, nodes[0].ClosedPosition);
            Assert.AreEqual(-1, nodes[1].ClosedPosition);
        }
    }
}
