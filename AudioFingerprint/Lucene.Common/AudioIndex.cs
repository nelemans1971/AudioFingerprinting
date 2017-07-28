using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Shingle;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using IOffsetAttribute = Lucene.Net.Analysis.Tokenattributes.IOffsetAttribute;
using IPositionIncrementAttribute = Lucene.Net.Analysis.Tokenattributes.IPositionIncrementAttribute;
using ITermAttribute = Lucene.Net.Analysis.Tokenattributes.ITermAttribute;
using Token = Lucene.Net.Analysis.Token;

namespace CDR.Indexer
{
    public class DefaultSimilarityExtended2 : Lucene.Net.Search.DefaultSimilarity
    {
        public override float ComputeNorm(System.String field, FieldInvertState state)
        {
            return state.Boost;
        }

        public override float Coord(int overlap, int maxOverlap)
        {
            return 1f / (float)maxOverlap;
        }

        public override float Idf(int docFreq, int numDocs)
        {
            return (float)(System.Math.Log(numDocs / (double)(docFreq + 1)) + 1.0);
            //return 1f;
        }


        public override float LengthNorm(System.String fieldName, int numTerms)
        {
            return (float)(1.0);
        }

        public override float QueryNorm(float sumOfSquaredWeights)
        {
            return (float)(1.0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="freq"></param>
        /// <returns></returns>
        public override float Tf(float freq)
        {
            return (freq == 0f) ? 0f : 1f;
        }
    }

    public class SimilarityNoPriority : Lucene.Net.Search.DefaultSimilarity
    {
        public override float ComputeNorm(System.String field, FieldInvertState state)
        {
            return (float)(1.0);
        }

        public override float Coord(int overlap, int maxOverlap)
        {
            return 1f;
        }

        public override float Idf(int docFreq, int numDocs)
        {
            return (float)(1.0);
        }


        public override float LengthNorm(System.String fieldName, int numTerms)
        {
            return (float)(1.0);
        }

        public override float QueryNorm(float sumOfSquaredWeights)
        {
            return (float)(1.0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="freq"></param>
        /// <returns></returns>
        public override float Tf(float freq)
        {
            return (freq == 0f) ? 0f : 1f;
        }
    }
}
