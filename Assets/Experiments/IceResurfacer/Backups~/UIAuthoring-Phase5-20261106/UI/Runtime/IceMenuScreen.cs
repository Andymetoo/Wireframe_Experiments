using UnityEngine;
using UnityEngine.UI;

namespace IceResurfacer.Presentation
{
    public enum IceMenuScreenKind { None, Welcome, Pause, Help, Results }
    /// <summary>Saved visual root; its visibility follows the existing game state.</summary>
    [DisallowMultipleComponent]
    public sealed class IceMenuScreen : MonoBehaviour
    {
        public IceMenuScreens owner;
        public IceMenuScreenKind kind;
        public CanvasGroup visibility;
        public Selectable firstSelection;
        [Tooltip("Explicit Tab order. Rearranging visuals never changes a command or result binding.")]
        public Selectable[] navigationOrder;
        public GameObject previewBadge;
        [System.NonSerialized] public GameObject lastSelection;
    }
}
