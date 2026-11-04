using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Keeps an authored part and its children out of runtime material batching,
    /// e.g. a replacement part that needs its own renderer during Play.</summary>
    [DisallowMultipleComponent]
    public sealed class IceKeepUnbatched : MonoBehaviour { }
}
