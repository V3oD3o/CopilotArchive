namespace CopilotArchiveSearch.Markdown
{
   using Markdig;
   using Markdig.Renderers;
   using Markdig.Syntax;
   using System.IO;

   public static class MarkdownRenderer
   {
      private static readonly MarkdownPipeline Pipeline =
          new MarkdownPipelineBuilder()
              .UseAdvancedExtensions()
              .UseYamlFrontMatter()
              .DisableHtml()
              .Build();

      public static void Render(string markdown, TextWriter writer)
      {
         var document = Markdown.Parse(markdown, Pipeline);
         var renderer = new HtmlRenderer(writer);

         writer.WriteLine("<!DOCTYPE html>");
         writer.WriteLine("<html>");
         writer.WriteLine("<head>");
         writer.WriteLine("<meta http-equiv='Content-Type' content='text/html; charset=utf-8'>");

         // Inject CSS from resource
         var css = LoadCss();
         writer.WriteLine("<style>");
         writer.WriteLine(css);
         writer.WriteLine("</style>");
         
         writer.WriteLine("</head>");
         writer.WriteLine("<body>");

         renderer.Render(document);

         writer.WriteLine("</body>");
         writer.WriteLine("</html>");
      }

      private static string LoadCss()
      {
         var asm = typeof(MarkdownRenderer).Assembly;
         using var stream = asm.GetManifestResourceStream("CopilotArchiveSearch.Resources.markdown-dark.css");
         if (stream == null)
         {
            return "body { font-family: 'Segoe UI', sans-serif; margin: 20px; }";
         }
         using var reader = new StreamReader(stream);
         return reader.ReadToEnd();
      }
   }
}