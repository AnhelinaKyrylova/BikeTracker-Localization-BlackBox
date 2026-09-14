using System;
using BikeTracker.Core.Interfaces;
using BikeTracker.Core.Models;

namespace BikeTracker.Core.Filters
{
    public class ParticleFilter : ILocalizationFilter
    {
        private readonly RssiPathLossModel _model;
        private readonly int _particleCount;
        private readonly double _processNoiseStd;
        private readonly double _measurementNoiseStd;
        private readonly Random _random;

        private double[] _particles;
        private double[] _weights;
        private long _lastTimestampMs = -1;
        private bool _initialized = false;

        public ParticleFilter(
            RssiPathLossModel model = null,
            int particleCount = 500,
            double processNoiseStd = 0.3,
            double measurementNoiseStd = 4.0,
            int? randomSeed = null)
        {
            _model = model ?? new RssiPathLossModel();
            _particleCount = particleCount;
            _processNoiseStd = processNoiseStd;
            _measurementNoiseStd = measurementNoiseStd;
            _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();

            _particles = new double[_particleCount];
            _weights = new double[_particleCount];
        }

        public void Reset()
        {
            _initialized = false;
            _lastTimestampMs = -1;
        }

        public PositionEstimate Update(RssiSample sample)
        {
            if (!_initialized)
            {
                double initialGuess = _model.EstimateDistance(sample.Rssi);
                for (int i = 0; i < _particleCount; i++)
                {
                    // rozptylime pociatocne particles okolo hrubeho odhadu
                    _particles[i] = Math.Max(0, initialGuess + NextGaussian() * 2.0);
                    _weights[i] = 1.0 / _particleCount;
                }
                _lastTimestampMs = sample.TimestampMs;
                _initialized = true;

                return new PositionEstimate
                {
                    DistanceM = initialGuess,
                    HeadingDeg = 0,
                    Confidence = 0.3
                };
            }

            double dt = (sample.TimestampMs - _lastTimestampMs) / 1000.0;
            if (dt <= 0) dt = 0.1;
            _lastTimestampMs = sample.TimestampMs;

            // --- PREDICT: random walk s process noise umernym dt ---
            for (int i = 0; i < _particleCount; i++)
            {
                _particles[i] += NextGaussian() * _processNoiseStd * Math.Sqrt(dt);
                if (_particles[i] < 0) _particles[i] = 0;
            }

            // --- UPDATE: vahy podla pravdepodobnosti namerania daneho RSSI ---
            double weightSum = 0;
            for (int i = 0; i < _particleCount; i++)
            {
                double predictedRssi = _model.PredictRssi(_particles[i]);
                double residual = sample.Rssi - predictedRssi;
                double likelihood = Math.Exp(-0.5 * (residual * residual) / (_measurementNoiseStd * _measurementNoiseStd));
                _weights[i] = likelihood + 1e-12; // ochrana pred nulovou vahou
                weightSum += _weights[i];
            }

            for (int i = 0; i < _particleCount; i++)
                _weights[i] /= weightSum;

            // --- ESTIMATE: vahovany priemer ---
            double estimatedDistance = 0;
            for (int i = 0; i < _particleCount; i++)
                estimatedDistance += _particles[i] * _weights[i];

            // --- Effective sample size (miera degeneracie -> confidence) ---
            double sumSquaredWeights = 0;
            for (int i = 0; i < _particleCount; i++)
                sumSquaredWeights += _weights[i] * _weights[i];
            double effectiveSampleSize = 1.0 / sumSquaredWeights;
            double confidence = effectiveSampleSize / _particleCount;

            // --- RESAMPLE (systematic resampling) ak degeneracia klesne pod polovicu ---
            if (effectiveSampleSize < _particleCount / 2.0)
            {
                Resample();
            }

            return new PositionEstimate
            {
                DistanceM = estimatedDistance,
                HeadingDeg = 0,
                Confidence = confidence
            };
        }

        private void Resample()
        {
            double[] newParticles = new double[_particleCount];
            double step = 1.0 / _particleCount;
            double start = _random.NextDouble() * step;
            double cumulative = _weights[0];
            int j = 0;

            for (int i = 0; i < _particleCount; i++)
            {
                double target = start + i * step;
                while (target > cumulative && j < _particleCount - 1)
                {
                    j++;
                    cumulative += _weights[j];
                }
                newParticles[i] = _particles[j];
            }

            _particles = newParticles;
            for (int i = 0; i < _particleCount; i++)
                _weights[i] = 1.0 / _particleCount;
        }

        // Box-Muller transformacia - generuje normalne rozdeleny sum z Random (ktory dava len uniformne)
        private double NextGaussian()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = _random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
    }
}