using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TryML
{
    /// <summary>
    /// image prediction class
    /// </summary>
    public class ImagePrediction : ImageData
    {
        /// <summary>
        /// confidence percentage for a give image classification
        /// </summary>
        public float[] Score;

        /// <summary>
        /// a value for the predicted image classification label
        /// </summary>
        public string PredictedLabelValue;
    }
}
