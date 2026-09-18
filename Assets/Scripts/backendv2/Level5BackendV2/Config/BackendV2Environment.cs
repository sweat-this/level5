namespace Level5.BackendV2
{
    /// <summary>Which Backend V2 deployment a config points at. Diagnostics/logging only, never
    /// used to branch client behavior - that would make one build's V2 client secretly two.</summary>
    public enum BackendV2Environment
    {
        Development = 0,
        Staging = 1,
        Production = 2
    }
}
