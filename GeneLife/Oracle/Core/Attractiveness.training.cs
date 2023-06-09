﻿// This file was auto-generated by ML.NET Model Builder.

using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace GeneLife.Oracle.Core
{
    internal partial class Attractiveness
    {
        public const string RetrainFilePath =  @"D:\Dev\GeneLife\GeneLife\Data\MLDataset\RelationDataSet.csv";
        public const char RetrainSeparatorChar = ',';
        public const bool RetrainHasHeader =  true;

         /// <summary>
        /// Train a new model with the provided dataset.
        /// </summary>
        /// <param name="outputModelPath">File path for saving the model. Should be similar to "C:\YourPath\ModelName.mlnet"</param>
        /// <param name="inputDataFilePath">Path to the data file for training.</param>
        /// <param name="separatorChar">Separator character for delimited training file.</param>
        /// <param name="hasHeader">Boolean if training file has a header.</param>
        public static void Train(string outputModelPath, string inputDataFilePath = RetrainFilePath, char separatorChar = RetrainSeparatorChar, bool hasHeader = RetrainHasHeader)
        {
            var mlContext = new MLContext();

            var data = LoadIDataViewFromFile(mlContext, inputDataFilePath, separatorChar, hasHeader);
            var model = RetrainModel(mlContext, data);
            SaveModel(mlContext, model, data, outputModelPath);
        }

        /// <summary>
        /// Load an IDataView from a file path.
        /// </summary>
        /// <param name="mlContext">The common context for all ML.NET operations.</param>
        /// <param name="inputDataFilePath">Path to the data file for training.</param>
        /// <param name="separatorChar">Separator character for delimited training file.</param>
        /// <param name="hasHeader">Boolean if training file has a header.</param>
        /// <returns>IDataView with loaded training data.</returns>
        public static IDataView LoadIDataViewFromFile(MLContext mlContext, string inputDataFilePath, char separatorChar, bool hasHeader)
        {
            return mlContext.Data.LoadFromTextFile<ModelInput>(inputDataFilePath, separatorChar, hasHeader);
        }



        /// <summary>
        /// Save a model at the specified path.
        /// </summary>
        /// <param name="mlContext">The common context for all ML.NET operations.</param>
        /// <param name="model">Model to save.</param>
        /// <param name="data">IDataView used to train the model.</param>
        /// <param name="modelSavePath">File path for saving the model. Should be similar to "C:\YourPath\ModelName.mlnet.</param>
        public static void SaveModel(MLContext mlContext, ITransformer model, IDataView data, string modelSavePath)
        {
            // Pull the data schema from the IDataView used for training the model
            DataViewSchema dataViewSchema = data.Schema;

            using (var fs = File.Create(modelSavePath))
            {
                mlContext.Model.Save(model, dataViewSchema, fs);
            }
        }


        /// <summary>
        /// Retrains model using the pipeline generated as part of the training process.
        /// </summary>
        /// <param name="mlContext"></param>
        /// <param name="trainData"></param>
        /// <returns></returns>
        public static ITransformer RetrainModel(MLContext mlContext, IDataView trainData)
        {
            var pipeline = BuildPipeline(mlContext);
            var model = pipeline.Fit(trainData);

            return model;
        }


        /// <summary>
        /// build the pipeline that is used from model builder. Use this function to retrain model.
        /// </summary>
        /// <param name="mlContext"></param>
        /// <returns></returns>
        public static IEstimator<ITransformer> BuildPipeline(MLContext mlContext)
        {
            // Data process configuration with pipeline data transformations
            var pipeline = mlContext.Transforms.ReplaceMissingValues(new []{new InputOutputColumnPair(@"F_Age", @"F_Age"),new InputOutputColumnPair(@"S_Age", @"S_Age")})      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"F_EyeColor",outputColumnName:@"F_EyeColor"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"F_HairType",outputColumnName:@"F_HairType"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"F_Morphotype",outputColumnName:@"F_Morphotype"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"F_Intelligence",outputColumnName:@"F_Intelligence"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"F_HeightPotential",outputColumnName:@"F_HeightPotential"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"F_Sex",outputColumnName:@"F_Sex"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"F_BehaviorPropension",outputColumnName:@"F_BehaviorPropension"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"S_EyeColor",outputColumnName:@"S_EyeColor"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"S_HairType",outputColumnName:@"S_HairType"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"S_Morphotype",outputColumnName:@"S_Morphotype"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"S_Intelligence",outputColumnName:@"S_Intelligence"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"S_HeightPotential",outputColumnName:@"S_HeightPotential"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"S_Sex",outputColumnName:@"S_Sex"))      
                                    .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName:@"S_BehaviorPropension",outputColumnName:@"S_BehaviorPropension"))      
                                    .Append(mlContext.Transforms.Concatenate(@"Features", new []{@"F_Age",@"S_Age",@"F_EyeColor",@"F_HairType",@"F_Morphotype",@"F_Intelligence",@"F_HeightPotential",@"F_Sex",@"F_BehaviorPropension",@"S_EyeColor",@"S_HairType",@"S_Morphotype",@"S_Intelligence",@"S_HeightPotential",@"S_Sex",@"S_BehaviorPropension"}))      
                                    .Append(mlContext.Transforms.Conversion.MapValueToKey(outputColumnName:@"Result",inputColumnName:@"Result",addKeyValueAnnotationsAsText:false))      
                                    .Append(mlContext.MulticlassClassification.Trainers.OneVersusAll(binaryEstimator: mlContext.BinaryClassification.Trainers.LbfgsLogisticRegression(new LbfgsLogisticRegressionBinaryTrainer.Options(){L1Regularization=1F,L2Regularization=1F,LabelColumnName=@"Result",FeatureColumnName=@"Features"}), labelColumnName:@"Result"))      
                                    .Append(mlContext.Transforms.Conversion.MapKeyToValue(outputColumnName:@"PredictedLabel",inputColumnName:@"PredictedLabel"));

            return pipeline;
        }
    }
 }
