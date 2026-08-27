namespace Brx.CopilotArchiveSync.Conversation;

using System;
using System.Collections.Generic;

public class CopilotConversationBuilder
{
   private readonly List<CopilotMessage> _messages = new List<CopilotMessage>();
   
   public string Title { get; }

   public CopilotConversationBuilder(string title)
   {
      ArgumentException.ThrowIfNullOrWhiteSpace(title);

      Title = title;
   }

   public void AddMessage(CopilotMessage message)
   {
      if (message != null)
      {
         _messages.Add(message);
      }
   }

   public CopilotConversation Build()
   {
      return new CopilotConversation(Title, _messages.AsReadOnly());
   }
}
