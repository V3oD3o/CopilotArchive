namespace Brx.CopilotArchiveSync.Sync;

using System;
using System.Collections.Generic;
using System.IO;
using Brx.CopilotArchiveSync.Conversation;
using Brx.CopilotArchiveSync.Metadata;
using Brx.CopilotArchiveSync.Utils;

public sealed class SyncEngine
{
   private const string TempFileExt = ".tmp";

   private readonly string _localRoot;
   private readonly string _backupRoot;
   
   public bool IsBackupEnabled => (_backupRoot != null);

   public SyncEngine(string localRoot, string backupRoot)
   {
      _localRoot = ResolveDirectory(localRoot, nameof(localRoot));
      if (backupRoot == null)
      {
         _backupRoot = null;
      }
      else
      {
         _backupRoot = ResolveDirectory(backupRoot!, nameof(backupRoot));

         if (AreSameDirectory(_localRoot, _backupRoot!))
            throw new InvalidOperationException("localRoot and backupRoot must not be the same directory");
      }
   }

   // --------------------------------------------------------------------
   // PUBLIC ENTRY POINT
   // --------------------------------------------------------------------
   public void Run(Dictionary<string, CopilotConversation> remoteConversations, Action<string> logger)
   {
      // Load local finalized .md files
      var localFolder = CopilotConversationFolder.LoadFrom(_localRoot, "*.md", Source.Local);

      // Load backup folder
      CopilotConversationFolder backupFolder = null;
      if (IsBackupEnabled)
         backupFolder = CopilotConversationFolder.LoadFrom(_backupRoot!, "*.md", Source.Backup);

      foreach (var kv in remoteConversations)
      {
         var remote = kv.Value;

         logger?.Invoke($"Processing {remote.LastMessage.Time:yyyy-MM-dd} - {remote.Title}");

         // Compute identity hash (same as SaveTo)
         string identity = remote.IdentityHash;

         // Lookup local entry
         localFolder.Entries.TryGetValue(identity, out var localEntry);

         // Lookup backup entry
         CopilotConversationEntry backupEntry = null;
         backupFolder.Entries.TryGetValue(identity, out backupEntry);

         // Try skip BEFORE writing temp file
         if (localEntry != null && TrySkip(localEntry, remote))
         {
            logger?.Invoke("  NO CHANGE");
            continue;
         }

         // Write temp file (with ADS metadata)
         var tempEntry = CreateTempFile(remote);

         // If local exists, compare content hash
         if (localEntry != null)
         {
            if (localEntry.ContentHash == tempEntry.ContentHash && localEntry.FilePath + TempFileExt == tempEntry.FilePath)
            {
               File.Delete(tempEntry.FilePath);
               logger?.Invoke("  NO CHANGE");
               continue;
            }

            // Content differs -> version update
            bool backedUp = false;
            if (IsBackupEnabled)
            {
               backedUp = RotateBackup(localEntry, backupEntry);
            }
            logger?.Invoke(backedUp ? "  OVERWRITE" : "  BACKUP + OVERWRITE");
            OverwriteLocal(localEntry, tempEntry);
            localFolder.Entries[identity] = tempEntry;
            continue;
         }

         // No local version -> new conversation
         logger?.Invoke("  NEW");
         FinalizeTemp(tempEntry);
         localFolder.Entries[identity] = tempEntry;
      }
   }

   // --------------------------------------------------------------------
   // HEADER-BASED SKIP OPTIMIZATION
   // --------------------------------------------------------------------
   private static bool TrySkip(CopilotConversationEntry localEntry, CopilotConversation remote)
   {
      if (!StringComparer.OrdinalIgnoreCase.Equals(localEntry.FilePath, CopilotConversationEntry.GenerateFileName(remote)))
      {
         return false;
      }

      StructuredHeaderBlock headerBlock;

      using (var reader = File.OpenText(localEntry.FilePath))
      {
         try
         {
            headerBlock = StructuredHeaderBlock.LoadFrom(reader, StringComparer.OrdinalIgnoreCase);
         }
         catch (InvalidDataException)
         {
            return false;
         }
      }

      if (headerBlock == null)
         return false;

      // Format-Version
      if (!headerBlock.TryGet(CopilotConversation.HeaderNames.FormatVersion, out string localFmtStr))
         return false;

      if (!int.TryParse(localFmtStr, out int localFmt))
         return false;

      if (localFmt != CopilotConversation.FormatVersion)
         return false;

      // Message-Count
      if (!headerBlock.TryGet(CopilotConversation.HeaderNames.MessageCount, out string messageCount))
         return false;

      if (!int.TryParse(messageCount, out int localCount))
         return false;

      if (localCount != remote.Messages.Count)
         return false;

      // First-Message-Time
      if (!headerBlock.TryGet(CopilotConversation.HeaderNames.FirstMessageTime, out string localFirst))
         return false;

      string remoteFirst = remote.FirstMessage?.Time.ToString(CopilotConversation.HeaderTimeFormat);
      if (!StringComparer.Ordinal.Equals(localFirst, remoteFirst))
         return false;

      // Last-Message-Time
      if (!headerBlock.TryGet(CopilotConversation.HeaderNames.LastMessageTime, out string localLast))
         return false;

      string remoteLast = remote.LastMessage?.Time.ToString(CopilotConversation.HeaderTimeFormat);
      if (!StringComparer.Ordinal.Equals(localLast, remoteLast))
         return false;

      // All stable fields match -> skip
      return true;
   }

   // --------------------------------------------------------------------
   // CREATE TEMP FILE (.md.tmp)
   // --------------------------------------------------------------------
   private CopilotConversationEntry CreateTempFile(CopilotConversation conversation)
   {
      string fileName = CopilotConversationEntry.GenerateFileName(conversation) + TempFileExt;
      string filePath = Path.Combine(_localRoot, fileName);

      return conversation.SaveTo(filePath);
   }

   // --------------------------------------------------------------------
   // BACKUP ROTATION (max 1 previous version)
   // --------------------------------------------------------------------
   private bool RotateBackup(CopilotConversationEntry localEntry, CopilotConversationEntry backupEntry)
   {
      string localName = Path.GetFileName(localEntry.FilePath);
      string backupPath = Path.Combine(_backupRoot, localName);

      // If backup exists and content hash matches -> skip
      if (backupEntry != null && backupEntry.ContentHash == localEntry.ContentHash)
         return false;

      // Move local -> backup (if name is the same, then overwrite, otherwise we keep both to preserve filename history)
      SafeRenameFile(localEntry.FilePath, backupPath);
      return true;
   }

   // --------------------------------------------------------------------
   // FINALIZE TEMP -> .md
   // --------------------------------------------------------------------
   private static void FinalizeTemp(CopilotConversationEntry tempEntry)
   {
      string tempPath = tempEntry.FilePath;
      string finalPath = ChangeExtensionStrict(tempPath, CopilotConversation.OutputFileExt + TempFileExt, CopilotConversation.OutputFileExt);

      SafeRenameFile(tempPath, finalPath);

      tempEntry.FilePath = finalPath;
      tempEntry.Source = Source.Local;
   }

   // --------------------------------------------------------------------
   // OVERWRITE LOCAL WITH TEMP
   // --------------------------------------------------------------------
   private static void OverwriteLocal(CopilotConversationEntry localEntry, CopilotConversationEntry tempEntry)
   {
      string tempPath = tempEntry.FilePath;
      string finalPath = ChangeExtensionStrict(tempPath, CopilotConversation.OutputFileExt + TempFileExt, CopilotConversation.OutputFileExt);
      string oldPath = localEntry.FilePath;

      bool isRename = !StringComparer.Ordinal.Equals(oldPath, finalPath);

      SafeRenameFile(tempPath, oldPath);

      if (isRename)
      {
         SafeRenameFile(oldPath, finalPath);
      }

      tempEntry.FilePath = finalPath;
      tempEntry.Source = Source.Local;
   }

   // --------------------------------------------------------------------
   // PATH & DIRECTORY HELPERS
   // --------------------------------------------------------------------
   public static string ChangeExtensionStrict(string path, string oldSuffix, string newSuffix)
   {
      if (string.IsNullOrWhiteSpace(path))
         throw new ArgumentException("Path cannot be null, empty or whitespace only.", nameof(path));

      if (string.IsNullOrWhiteSpace(oldSuffix))
         throw new ArgumentException("Old suffix cannot be null, empty or whitespace only .", nameof(oldSuffix));

      if (!oldSuffix.StartsWith('.'))
         oldSuffix = "." + oldSuffix;

      if (!string.IsNullOrWhiteSpace(newSuffix) && !newSuffix.StartsWith('.'))
         newSuffix = "." + newSuffix;

      if (!path.EndsWith(oldSuffix, StringComparison.OrdinalIgnoreCase))
         throw new InvalidOperationException(
             $"Path '{path}' does not end with expected suffix '{oldSuffix}'."
         );

      return path.Substring(0, path.Length - oldSuffix.Length) + newSuffix;
   }


   private static string ResolveDirectory(string path, string parameterName)
   {
      if (string.IsNullOrWhiteSpace(path))
         throw new ArgumentException($"{parameterName} must not be null or empty.", parameterName);

      var fullPath = Path.GetFullPath(path);

      if (!Directory.Exists(fullPath))
         throw new DirectoryNotFoundException(fullPath);

      return fullPath;
   }

   private static void SafeRenameFile(string sourcePath, string destPath)
   {
      ClearReadOnlyIfExists(destPath);
      NativeFile.AtomicReplace(sourcePath, destPath);
   }

   private static void ClearReadOnlyIfExists(string path)
   {
      if (!File.Exists(path))
         return;

      var attr = File.GetAttributes(path);
      if ((attr & FileAttributes.ReadOnly) != 0)
      {
         File.SetAttributes(path, attr & ~FileAttributes.ReadOnly);
      }
   }

   private static bool AreSameDirectory(string pathA, string pathB)
   {
      var fullA = Path.GetFullPath(pathA);
      var fullB = Path.GetFullPath(pathB);

      if (StringComparer.OrdinalIgnoreCase.Equals(fullA, fullB))
         return true;

      var testFile = Guid.NewGuid().ToString("N") + TempFileExt;
      var fileInA = Path.Combine(fullA, testFile);
      var fileInB = Path.Combine(fullB, testFile);

      try
      {
         File.WriteAllText(fileInA, "x");
         if (File.Exists(fileInB))
         {
            File.Delete(fileInB);
            return true;
         }
         return false;
      }
      finally
      {
         if (File.Exists(fileInA)) File.Delete(fileInA);
         if (File.Exists(fileInB)) File.Delete(fileInB);
      }
   }
}
