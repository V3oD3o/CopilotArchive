using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;

namespace CopilotArchiveSearch.Search
{
   public static class IndexedSearch
   {
      public enum QueryMode
      {
         RunAsIs,
         RunAsPrefix,
         DontRun
      }

      public static QueryMode GetQueryMode(string query)
      {
         if (string.IsNullOrWhiteSpace(query))
            return QueryMode.DontRun;

         query = query.Trim();

         // 1. Unclosed quotes -> incomplete phrase
         int quoteCount = query.Count(c => c == '"');
         if (quoteCount % 2 == 1)
            return QueryMode.DontRun;

         // Tokenize
         var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
         string lastToken = tokens[^1];

         // 2. Last token is an operator -> incomplete expression
         string[] ops = { "AND", "OR", "NOT", "NEAR" };
         if (ops.Contains(lastToken.ToUpperInvariant()))
            return QueryMode.DontRun;

         // 3. If last char is alphanumeric -> prefix search
         char lastChar = query[^1];
         if (char.IsLetterOrDigit(lastChar))
            return QueryMode.RunAsPrefix;

         // 4. Otherwise -> run as-is
         return QueryMode.RunAsIs;
      }
      
      public static IEnumerable<SearchResult> Search(string folder, string query, bool queryAsPrefix)
      {
         using var conn = new OleDbConnection(
             "Provider=Search.CollatorDSO;Extended Properties='Application=Windows';");

         conn.Open();

         string path = Path.TrimEndingDirectorySeparator(folder).Replace(Path.DirectorySeparatorChar, '/') + '/';
         string contains = queryAsPrefix ? $"\"{query}*\"" : $"\"{query}\"";
         string sql = $@"
                SELECT 
                    System.ItemPathDisplay,
                    System.Search.Rank
                FROM SYSTEMINDEX
                WHERE SCOPE='file:{path}'
                  AND CONTAINS('{contains}')
                ORDER BY System.Search.Rank DESC
            ";

         using var cmd = new OleDbCommand(sql, conn);
         using var reader = cmd.ExecuteReader();

         while (reader.Read())
         {
            yield return new SearchResult
            {
               Path = reader.GetString(0),
               Rank = reader.GetInt32(1)
            };
         }
      }
   }
}