using TMPro;
using UnityEngine;

namespace IceResurfacer.Presentation
{
    public sealed class IceMessageHistoryRow : MonoBehaviour
    {
        public TMP_Text content;
        public void Bind(string message) => IceNavigationUI.Text(content, message);
    }
}
