namespace ReactiveDomain
{
    /// <summary>
    /// Converts a stream name into a converted stream name.
    /// </summary>
    /// <param name="name">The stream name to convert.</param>
    /// <returns>A converted stream name.</returns>
    /// <remarks>The reason for this concept is so that 
    /// - prefixing (component or context),
    /// - suffixing,
    /// - fixing the casing of,
    /// - etc...
    /// of a stream name become possible.</remarks>
    public delegate StreamName StreamNameConverter(StreamName name);
}