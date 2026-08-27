namespace Brx.CopilotArchiveSync.Conversation;

using System;

public class CopilotMessage
{
   public DateTime Time { get; set; }
   public string Message { get; set; }
   public string Author { get; set; }

   public CopilotMessage() { }
}
