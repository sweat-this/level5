using System.Runtime.CompilerServices;

// Grants Assets/Tests/Editor (which has no asmdef of its own and compiles into
// Assembly-CSharp-Editor, not this file's Assembly-CSharp) access to DBHelper/DBConnector's
// internal test-only seams (ConfigureForTests, createDatabase, CreateLocalProfileCoroutine,
// Connection) without widening any of them to public. Mirrors
// Assets/Scripts/player/Level5Player/AssemblyInfo.cs's [assembly: InternalsVisibleTo("Assembly-CSharp")],
// just in the opposite direction.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
