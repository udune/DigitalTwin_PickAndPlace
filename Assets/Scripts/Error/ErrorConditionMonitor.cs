using UnityEngine;

    public class ErrorConditionMonitor : MonoBehaviour
    {
        [Header("Axis References")]
        public Transform xAxis;
        public Transform yAxis;
        public Transform zAxis;

        [Header("Limits")]
        public float xMin = -5f;
        public float xMax = 5f;
        public float yMin = -5f;
        public float yMax = 5f;
        public float zMin = -5f;
        public float zMax = 5f;

        [Header("Settings")]
        [Range(0f, 1f)] 
        public float warningThreshold = 0.85f;
        private const float Hysteresis = 0.02f;

        private void Update()
        {
            if (ErrorManager.Instance == null)
            {
                return;
            }

            CheckAxis(xAxis, xAxis.localPosition.x, xMin, xMax, ErrorSource.XAxis, "XAxis", "X-AXIS");
            CheckAxis(yAxis, yAxis.localPosition.z, yMin, yMax, ErrorSource.YAxis, "YAxis", "Y-AXIS");
            CheckAxis(zAxis, zAxis.localPosition.y, zMin, zMax, ErrorSource.ZAxis, "ZAxis", "Z-AXIS");
        }

        private void CheckAxis(Transform axis, float currentValue, float min, float max, ErrorSource source, string idPrefix, string locationName)
        {
            if (axis == null)
            {
                return;
            }

            float range = max - min;
            float warningMargin = range * (1.0f - warningThreshold) * 0.5f;
            
            bool isErrorMin = currentValue < min;
            bool isErrorMax = currentValue > max;
            bool isWarningMin = currentValue < (min + warningMargin) && !isErrorMin;
            bool isWarningMax = currentValue > (max - warningMargin) && !isErrorMax;

            string errorId = $"{idPrefix}_Limit";
            string warningId = $"{idPrefix}_Warning";

            if (isErrorMin || isErrorMax)
            {
                ErrorManager.Instance.RaiseError(new ErrorInfo
                {
                    Id = errorId,
                    Type = ErrorType.Error,
                    Source = source,
                    Location = locationName,
                    Message = $"Limit Exceeded: {currentValue:F2}",
                    Timestamp = Time.time
                });
                
                ErrorManager.Instance.ClearError(warningId);
            }
            else
            {
                if (currentValue > min + Hysteresis && currentValue < max - Hysteresis)
                {
                    ErrorManager.Instance.ClearError(errorId);
                }
            }

            if ((isWarningMin || isWarningMax) && !ErrorManager.Instance.ErrorInfoList.Exists(errorInfo => errorInfo.Id == errorId))
            {
                ErrorManager.Instance.RaiseError(new ErrorInfo
                {
                    Id = warningId,
                    Type = ErrorType.Warning,
                    Source = source,
                    Location = locationName,
                    Message = $"Approaching Limit: {currentValue:F2}",
                    Timestamp = Time.time
                });
            }
            else
            {
                bool clearMin = currentValue > (min + warningMargin + Hysteresis);
                bool clearMax = currentValue < (max - warningMargin - Hysteresis);
                
                if (clearMin && clearMax)
                {
                    ErrorManager.Instance.ClearError(warningId);
                }
            }
        }
    }