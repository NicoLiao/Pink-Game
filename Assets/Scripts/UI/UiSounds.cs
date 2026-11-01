using System.Collections.Generic;
using LoveShot.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoveShot
{
    // One per scene: plays a hover sound when the mouse moves onto any usable button or toggle and a click sound when
    // it is pressed, so buttons need no sound component of their own (including ones instantiated at run time).
    public class UiSounds : MonoBehaviour
    {
        [SerializeField] GameAudio gameAudio;
        [SerializeField] Sound hover;
        [SerializeField] Sound click;

        readonly List<RaycastResult> hits = new();
        PointerEventData pointer;
        Selectable over;

        void Update()
        {
            EventSystem events = EventSystem.current;
            Mouse mouse = Mouse.current;
            if (gameAudio == null || events == null || mouse == null) return;

            if (pointer == null) pointer = new PointerEventData(events);
            pointer.position = mouse.position.ReadValue();
            hits.Clear();
            events.RaycastAll(pointer, hits);

            Selectable now = null;
            if (hits.Count > 0)
            {
                Selectable found = hits[0].gameObject.GetComponentInParent<Selectable>();
                if (found != null && found.interactable && (found is Button || found is Toggle)) now = found;
            }
            if (now != over)
            {
                over = now;
                if (now != null) gameAudio.Play(hover);
            }
            if (now != null && mouse.leftButton.wasPressedThisFrame) gameAudio.Play(click);
        }
    }
}
