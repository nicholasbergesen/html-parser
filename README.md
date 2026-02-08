# Nick.HtmlParser

A lightweight, dependency-free HTML parser for .NET that converts HTML into a flat, typed `IEnumerable<INode>` structure. Ideal for scenarios where you need to quickly search, filter, or traverse parsed HTML without the overhead of a full DOM tree.

[![NuGet](https://img.shields.io/nuget/v/Nick.HtmlParser)](https://www.nuget.org/packages/Nick.HtmlParser)

## Features

- **Flat structure** – Parses HTML into a flat list of `INode` objects, making it easy to query with LINQ.
- **Parent & child references** – Each node exposes `Parent` and `Children` properties for tree-style navigation when needed.
- **Attribute parsing** – Extracts tag attributes into a `Dictionary<string, string>`.
- **Typed nodes** – Every node has a `NodeType` enum value for fast type checking (e.g., `NodeType.div`, `NodeType.a`, `NodeType.img`).
- **Void / self-closing tag support** – Correctly handles `<br>`, `<img>`, `<input>`, `<meta>`, and all other HTML void elements.
- **Script & style skipping** – Automatically skips `<script>` and `<style>` tag contents (including nested script tags) to avoid parse errors from `<` characters in code.
- **Comment & DOCTYPE handling** – Comments (`<!-- -->`), `<!DOCTYPE>`, and XML processing instructions (`<?xml ?>`) are skipped during parsing.
- **Malformed HTML recovery** – Gracefully recovers from common issues such as missing closing tags, rogue closing tags without openers, and malformed attribute quotes.
- **Optional raw content loading** – Pass `loadContent: true` to retain the original HTML text of each node.
- **Zero dependencies** – Only relies on the .NET base class library.
- **Targets .NET 10**

## Installation

Install via NuGet:

```
dotnet add package Nick.HtmlParser
```

Or via the NuGet Package Manager:

```
Install-Package Nick.HtmlParser
```

## Quick Start

```csharp
using HtmlParser;

var html = "<html><body><div class=\"container\"><p>Hello World</p></div></body></html>";

// Parse without loading raw content
IReadOnlyList<INode> nodes = Parser.Parse(html);

// Parse with raw content loaded into each node
IReadOnlyList<INode> nodesWithContent = Parser.Parse(html, loadContent: true);
```

## API Reference

### `Parser.Parse(string html, bool loadContent = false)`

Parses an HTML string and returns an `IReadOnlyList<INode>`.

| Parameter     | Type   | Default | Description |
|---------------|--------|---------|-------------|
| `html`        | `string` | —     | The HTML string to parse. |
| `loadContent` | `bool`   | `false` | When `true`, populates each node's `Content` property with the raw HTML text. Uses more memory. |

### `INode` Interface

| Property     | Type                           | Description |
|--------------|--------------------------------|-------------|
| `Name`       | `string`                       | The tag name (e.g., `"div"`, `"a"`, `"img"`). |
| `Type`       | `NodeType`                     | The parsed enum type of the tag. Returns `NodeType.unknown` for non-standard tags. |
| `Content`    | `string?`                      | The raw HTML of the node. Only populated when `loadContent` is `true`. |
| `Attributes` | `Dictionary<string, string>`   | The tag's attributes as key-value pairs. |
| `OpenPosition`  | `int`                       | Character position of the opening `<` in the source HTML. |
| `ClosedPosition` | `int`                      | Character position of the closing `>` in the source HTML. `-1` if unclosed. |
| `Depth`      | `int`                          | The nesting depth of the node (0-based). |
| `Parent`     | `INode?`                       | Reference to the parent node, or `null` for top-level nodes. |
| `Children`   | `IReadOnlyCollection<INode>?`  | Direct child nodes, or `null` if the node has no children. |

### `NodeType` Enum

Contains values for all standard HTML tags (`div`, `p`, `a`, `img`, `span`, `table`, etc.) plus `unknown` for unrecognized tags.

## Usage Examples

### Find all links

```csharp
var links = nodes.Where(n => n.Type == NodeType.a);
foreach (var link in links)
{
    if (link.Attributes.TryGetValue("href", out var href))
        Console.WriteLine(href);
}
```

### Find nodes by depth

```csharp
// Get all top-level nodes
var topLevel = nodes.Where(n => n.Depth == 0);
```

### Navigate parent/child relationships

```csharp
var divs = nodes.Where(n => n.Type == NodeType.div);
foreach (var div in divs)
{
    Console.WriteLine($"Div at depth {div.Depth} has {div.Children?.Count ?? 0} children");
    if (div.Parent != null)
        Console.WriteLine($"  Parent: {div.Parent.Name}");
}
```

### Get raw HTML content

```csharp
var nodesWithContent = Parser.Parse(html, loadContent: true);
var firstDiv = nodesWithContent.First(n => n.Type == NodeType.div);
Console.WriteLine(firstDiv.Content); // e.g. <div class="container"><p>Hello World</p></div>
```

### Find elements by attribute

```csharp
var elementsWithClass = nodes.Where(n => n.Attributes.ContainsKey("class"));
var specificClass = nodes.Where(n =>
    n.Attributes.TryGetValue("class", out var cls) && cls.Contains("container"));
```

## Supported Void (Self-Closing) Tags

The following tags are treated as self-closing and will not look for a closing tag:

`area`, `base`, `br`, `col`, `command`, `embed`, `hr`, `img`, `input`, `keygen`, `link`, `meta`, `param`, `source`, `track`, `wbr`

## Limitations

- **Script & style content is skipped** – The parser does not produce nodes for content inside `<script>` or `<style>` tags, though the tags themselves are captured.
- **No CSS selector support** – Use LINQ to query the flat node list instead.
- **No modification / serialization** – This is a read-only parser; it does not support modifying or serializing HTML back to a string.
- **Error reporting** – Parsing errors (e.g., duplicate attributes, unclosed tags) are silently handled rather than reported. Error reporting is planned for a future release.

## Building & Testing

```bash
# Build the library
dotnet build Nick.HtmlParser/Nick.HtmlParser.csproj

# Run tests
dotnet test Test/Test.csproj
```

## License

See [LICENSE](LICENSE) for details.

## Changelog

### v1.0.9
- Bugfix: Improved bounds checking to prevent crashes on malformed HTML (unterminated comments, unclosed tags, lone chevrons).
- Bugfix: Fixed handling of unterminated DOCTYPE and XML processing instructions.
- Upgrade to .NET 10.
- Increased test coverage for edge cases.

### v1.0.8
- Bugfix: Handle attributes that contain tags, including malformed attributes.
- Upgrade to .NET 8.0.

### v1.0.7
- Reference parent and child nodes from `INode`.

### v1.0.6
- Skip `<script>` and `<style>` tag contents.
- Bugfix: Handle nested script tags.

### v1.0.4
- Cater for non-standard HTML tags.
- Ignore XML processing instructions.
- Improve parsing recovery for malformed HTML documents.
- Load raw node text into parsed `INode` objects (opt-in via `loadContent`).
