namespace Memoria.Accessibility.Navigation
{
    // The native caller validates all four current polygons/permissions before
    // consulting this cache. Geometry fingerprints include floors, flags and
    // player radius, so a changed surface cannot reuse an old set of points.
    internal sealed class OpeningCandleApproachCache
    {
        private long _generation;
        private string _fingerprint;
        private NavigationPoint[] _points;
        internal NavigationPoint[] Read(FieldGeometrySnapshot geometry)
        {
            if(geometry==null)return new NavigationPoint[0];
            if(_points==null || geometry.Fingerprint==null || _generation!=geometry.Generation || _fingerprint!=geometry.Fingerprint){
                _generation=geometry.Generation;_fingerprint=geometry.Fingerprint;
                _points=FieldRegionApproaches.Interaction(OpeningCandleEvidence.ApproachArea,geometry);
            }
            return _points;
        }
    }
}
