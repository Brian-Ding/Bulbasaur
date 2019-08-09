using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TryML
{
    /// <summary>
    /// input image data class
    /// </summary>
    public class ImageData
    {
        /// <summary>
        /// image file name
        /// </summary>
        [LoadColumn(0)]
        public string ImagePath;

        /// <summary>
        /// value for the image label
        /// </summary>
        [LoadColumn(1)]
        public string Label;
    }
}
