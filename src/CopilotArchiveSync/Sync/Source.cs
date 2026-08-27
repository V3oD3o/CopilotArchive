namespace Brx.CopilotArchiveSync.Sync;

public enum Source
{
   Remote, // From CSV: generated file name, identityHash, contentHash, contentSize
   Local,  // From disk: actual file name, identityHash, contentHash, contentSize
   Temp,   // From temp write: temp file name, identityHash, contentHash, contentSize
   Backup
}
