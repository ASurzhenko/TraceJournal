using UnityEngine;

namespace TraceJournal.UI
{
    public sealed class LoadingCircle : MonoBehaviour
    {
        private static readonly float FillSpeedPerSecond = 0.6f;

        [SerializeField] private UnityEngine.UI.Image _circleImage;

        private bool _isBackFill;

        private void Update()
        {
            float direction = _isBackFill ? -1f : 1f;
            _circleImage.fillAmount += direction * FillSpeedPerSecond * Time.unscaledDeltaTime;

            if (_isBackFill && _circleImage.fillAmount <= 0f)
            {
                _circleImage.fillAmount = 0f;
                _circleImage.fillClockwise = true;
                _isBackFill = false;
            }
            else if (!_isBackFill && _circleImage.fillAmount >= 1f)
            {
                _circleImage.fillAmount = 1f;
                _circleImage.fillClockwise = false;
                _isBackFill = true;
            }
        }
    }
}
