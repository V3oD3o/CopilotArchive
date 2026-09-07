namespace Brx.CopilotArchiveSearch.Search;

public class SearchResult
{
   public string? Path { get; set; }
   public int Rank { get; set; }

   public string Display => (Path == null) ? "<null>" : System.IO.Path.GetFileName(Path);
}