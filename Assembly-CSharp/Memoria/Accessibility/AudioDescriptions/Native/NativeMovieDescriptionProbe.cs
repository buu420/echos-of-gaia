using Assets.Sources.Graphics.Movie;
using UnityEngine;
namespace Memoria.Accessibility.AudioDescriptions
{
    // Actual render observation; creating/loading a material does not prove
    // that a film was visible to the player.
    public sealed class NativeMovieDescriptionProbe : MonoBehaviour
    {
        private MovieMaterial _movie;
        private Renderer _renderer;
        private Camera _camera;
        public static void Attach(GameObject plane,MovieMaterial movie,Camera camera)
        {
            try{var probe=plane.GetComponent<NativeMovieDescriptionProbe>()??plane.AddComponent<NativeMovieDescriptionProbe>();probe._movie=movie;probe._renderer=plane.GetComponent<Renderer>();probe._camera=camera;NativeAudioDescriptions.Initialize();}catch(System.Exception exception){Memoria.Prime.Log.Message("[FF9Access] Movie description observer unavailable: {0}",exception.Message);}
        }
        public static void SetCamera(GameObject plane,Camera camera){try{var probe=plane.GetComponent<NativeMovieDescriptionProbe>();if(probe!=null)probe._camera=camera;}catch(System.Exception exception){Memoria.Prime.Log.Message("[FF9Access] Movie description camera unavailable: {0}",exception.Message);}}
        private void OnWillRenderObject()
        {
            if(_movie==null || _renderer==null || !_renderer.enabled || !gameObject.activeInHierarchy || _camera==null || Camera.current!=_camera)return;
            NativeAudioDescriptions.MoviePresented(_movie);
        }
    }
}
