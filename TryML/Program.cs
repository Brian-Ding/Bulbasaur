using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TryML
{
    class Program
    {
        // DeclareGlobalVariables
        static readonly string _assetsPath = Path.Combine(Environment.CurrentDirectory, "assets");
        static readonly string _trainTagsTsv = Path.Combine(_assetsPath, "inputs-train", "data", "tags.tsv");
        static readonly string _predictImageListTsv = Path.Combine(_assetsPath, "inputs-predict", "data", "image_list.tsv");
        static readonly string _trainImagesFolder = Path.Combine(_assetsPath, "inputs-train", "data");
        static readonly string _predictImagesFolder = Path.Combine(_assetsPath, "inputs-predict", "data");
        static readonly string _predictSingleImage = Path.Combine(_assetsPath, "inputs-predict-single", "data", "Absol.jpg");
        static readonly string _inceptionPb = Path.Combine(_assetsPath, "inputs-train", "inception", "tensorflow_inception_graph.pb");
        static readonly string _inputImageClassifierZip = Path.Combine(_assetsPath, "inputs-predict", "imageClassifier.zip");
        static readonly string _outputImageClassifierZip = Path.Combine(_assetsPath, "outputs", "imageClassifier.zip");
        private static string LabelTokey = nameof(LabelTokey);
        private static string PredictedLabelValue = nameof(PredictedLabelValue);

        static void Main(string[] args)
        {
            // Create MLContext to be shared across the model creation workflow objects 
            // CreateMLContext
            MLContext mlContext = new MLContext(seed: 1);

            // CallReuseAndTuneInceptionModel
            var model = ReuseAndTuneInceptionModel(mlContext, _trainTagsTsv, _trainImagesFolder, _inceptionPb, _outputImageClassifierZip);

            // CallClassifyImages
            ClassifyImages(mlContext, _predictImageListTsv, _predictImagesFolder, _outputImageClassifierZip, model);

            // CallClassifySingleImage
            ClassifySingleImage(mlContext, _predictSingleImage, _outputImageClassifierZip, model);
        }

        // Build and train model
        public static ITransformer ReuseAndTuneInceptionModel(MLContext mlContext, string dataLocation, string imagesFolder, string inputModelLocation, string outputModelLocation)
        {

            // LoadData
            var data = mlContext.Data.LoadFromTextFile<ImageData>(path: dataLocation, hasHeader: false);

            // MapValueToKey1
            var estimator = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: LabelTokey, inputColumnName: "Label")
                            // The image transforms transform the images into the model's expected format.
                            // ImageTransforms
                            .Append(mlContext.Transforms.LoadImages(outputColumnName: "input", imageFolder: _trainImagesFolder, inputColumnName: nameof(ImageData.ImagePath)))
                            .Append(mlContext.Transforms.ResizeImages(outputColumnName: "input", imageWidth: InceptionSettings.ImageWidth, imageHeight: InceptionSettings.ImageHeight, inputColumnName: "input"))
                            .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "input", interleavePixelColors: InceptionSettings.ChannelsLast, offsetImage: InceptionSettings.Mean))
                            // The ScoreTensorFlowModel transform scores the TensorFlow model and allows communication 
                            // ScoreTensorFlowModel
                            .Append(mlContext.Model.LoadTensorFlowModel(inputModelLocation).
                                ScoreTensorFlowModel(outputColumnNames: new[] { "softmax2_pre_activation" }, inputColumnNames: new[] { "input" }, addBatchDimensionInput: true))
                            // AddTrainer 
                            .Append(mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(labelColumnName: LabelTokey, featureColumnName: "softmax2_pre_activation"))
                            // MapValueToKey2
                            .Append(mlContext.Transforms.Conversion.MapKeyToValue(PredictedLabelValue, "PredictedLabel"))
                            .AppendCacheCheckpoint(mlContext);

            // Train the model
            Console.WriteLine("=============== Training classification model ===============");
            // Create and train the model based on the dataset that has been loaded, transformed.
            // TrainModel
            ITransformer model = estimator.Fit(data);

            // Process the training data through the model
            // This is an optional step, but it's useful for debugging issues
            // TransformData
            var predictions = model.Transform(data);

            // Create enumerables for both the ImageData and ImagePrediction DataViews 
            // for displaying results
            // EnumerateDataViews
            var imageData = mlContext.Data.CreateEnumerable<ImageData>(data, false, true);
            var imagePredictionData = mlContext.Data.CreateEnumerable<ImagePrediction>(predictions, false, true);

            // CallDisplayResults1
            DisplayResults(imagePredictionData);

            // Get some performance metrics on the model using training data
            Console.WriteLine("=============== Classification metrics ===============");

            // Evaluate           
            var multiclassContext = mlContext.MulticlassClassification;
            var metrics = multiclassContext.Evaluate(predictions, labelColumnName: LabelTokey, predictedLabelColumnName: "PredictedLabel");

            //DisplayMetrics
            Console.WriteLine($"LogLoss is: {metrics.LogLoss}");
            Console.WriteLine($"PerClassLogLoss is: {String.Join(" , ", metrics.PerClassLogLoss.Select(c => c.ToString()))}");

            // ReturnModel
            return model;
        }

        public static void ClassifyImages(MLContext mlContext, string dataLocation, string imagesFolder, string outputModelLocation, ITransformer model)
        {

            // Read the image_list.tsv file and add the filepath to the image file name 
            // before loading into ImageData 
            // CallReadFromTSV 
            var imageData = ReadFromTsv(dataLocation, imagesFolder);
            var imageDataView = mlContext.Data.LoadFromEnumerable<ImageData>(imageData);

            // Predict  
            var predictions = model.Transform(imageDataView);
            var imagePredictionData = mlContext.Data.CreateEnumerable<ImagePrediction>(predictions, false, true);

            Console.WriteLine("=============== Making classifications ===============");

            // CallDisplayResults2
            DisplayResults(imagePredictionData);
        }

        public static void ClassifySingleImage(MLContext mlContext, string imagePath, string outputModelLocation, ITransformer model)
        {
            // load the fully qualified image file name into ImageData 
            // LoadImageData 
            var imageData = new ImageData()
            {
                ImagePath = imagePath
            };

            // PredictSingle  
            // Make prediction function (input = ImageData, output = ImagePrediction)
            var predictor = mlContext.Model.CreatePredictionEngine<ImageData, ImagePrediction>(model);
            var prediction = predictor.Predict(imageData);

            Console.WriteLine("=============== Making single image classification ===============");
            // DisplayPrediction
            Console.WriteLine($"Image: {Path.GetFileName(imageData.ImagePath)} predicted as: {prediction.PredictedLabelValue} with score: {prediction.Score.Max()} ");

        }

        private static void DisplayResults(IEnumerable<ImagePrediction> imagePredictionData)
        {
            // DisplayPredictions
            foreach (ImagePrediction prediction in imagePredictionData)
            {
                Console.WriteLine($"Image: {Path.GetFileName(prediction.ImagePath)} predicted as: {prediction.PredictedLabelValue} with score: {prediction.Score.Max()} ");
            }
        }

        public static IEnumerable<ImageData> ReadFromTsv(string file, string folder)
        {
            //Need to parse through the tags.tsv file to combine the file path to the 
            // image name for the ImagePath property so that the image file can be found.

            // ReadFromTsv
            return File.ReadAllLines(file)
             .Select(line => line.Split('\t'))
             .Select(line => new ImageData()
             {
                 ImagePath = Path.Combine(folder, line[0])
             });
        }
    }
}
