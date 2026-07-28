using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ProjectUI
{
    public class ToggleSwitch : MonoBehaviour, IPointerClickHandler
    {
        [Header("Slider Setup")]
        [SerializeField, Range(0, 1f)] private float sliderValue = 1f;
        public bool CurrentValue { get; private set; }
        private bool previousValue;
        private Slider slider;
        [Header("Animation")]
        [SerializeField, Range(0, 1f)] private float animationDuration = 0.125f;
        [SerializeField]
        private AnimationCurve slideEase =
            AnimationCurve.EaseInOut(0, 0, 1, 1);
        private Coroutine animateSliderCoroutine;
        [Header("Events")]
        [SerializeField] private UnityEvent OnToggleOn;
        [SerializeField] private UnityEvent OnToggleOff;

        private void SetupSliderComponent()
        {
            slider = GetComponent<Slider>();
            if (slider == null)
            {
                Debug.LogError("No Slider found!", this);
                return;
            }

            slider.interactable = false;
            var sliderColors = slider.colors;
            sliderColors.disabledColor = Color.white;
            slider.colors = sliderColors;
            slider.transition = Selectable.Transition.None;
            slider.value = sliderValue;
        }
        protected virtual void Awake()
        {
            SetupSliderComponent();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log("Clicked");
            Toggle();
        }
        private void Toggle()
        {
            SetStateAndStartAnimation(!CurrentValue);
        }
        private void SetStateAndStartAnimation(bool state)
        {
            previousValue = CurrentValue;
            CurrentValue = state;

            if (previousValue != CurrentValue)
            {
                if (CurrentValue)
                    OnToggleOn?.Invoke();
                else
                    OnToggleOff?.Invoke();
            }
            if (animateSliderCoroutine != null)
                StopCoroutine(animateSliderCoroutine);

            animateSliderCoroutine = StartCoroutine(AnimateSlider());
        }

        private IEnumerator AnimateSlider()
        {
            float startValue = slider.value;
            float endValue = CurrentValue ? 1 : 0;
            float time = 0;
            if (animationDuration > 0)
            {
                while (time < animationDuration)
                {
                    time += Time.deltaTime;
                    float lerpFactor = slideEase.Evaluate(time / animationDuration);
                    slider.value = sliderValue = Mathf.Lerp(startValue, endValue, lerpFactor);

                    yield return null;
                }
            }
            slider.value = endValue;
        }

    }
}
