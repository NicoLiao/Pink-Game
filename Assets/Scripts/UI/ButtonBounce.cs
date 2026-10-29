using UnityEngine;
using UnityEngine.EventSystems;

namespace LoveShot
{
    // Grows a button slightly while hovered or selected, and squishes it while pressed.
    public class ButtonBounce : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Min(0f)] float hoverScale = 1.08f;
        [SerializeField, Min(0f)] float pressedScale = 0.95f;
        [SerializeField, Min(0.01f)] float speed = 14f;

        bool hovered, selected, pressed;

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = pressed = false;
        public void OnSelect(BaseEventData eventData) => selected = true;
        public void OnDeselect(BaseEventData eventData) => selected = false;
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        void OnDisable()
        {
            hovered = selected = pressed = false;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float target = pressed ? pressedScale : hovered || selected ? hoverScale : 1f;
            float scale = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            transform.localScale = Vector3.one * scale;
        }
    }
}
