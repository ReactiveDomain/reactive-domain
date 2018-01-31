using System;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain
{
    public partial class Metadata
    {
        [Obsolete("This method is obsolete and will be removed in a future version. Please use the Metadata.With(Metadatum metadatum) method instead.")]
        public Metadata Add(Metadatum metadatum)
        {
            return this.With(metadatum);
        }

        [Obsolete("This method is obsolete and will be removed in a future version. Please use the Metadata.With(Metadatum[] metadata) method instead.")]
        public Metadata AddMany(Metadatum[] metadata)
        {
            return this.With(metadata);
        }
    }
}