using System;
using BikeTracker.Core.Interfaces;
using BikeTracker.Core.Models;

namespace BikeTracker.Core.Filters
{
    public class ExtendedKalmanFilter : ILocalizationFilter
    {
        private readonly RssiPathLossModel _model;
        private readonly double _processNoiseDistance;
        private readonly double _processNoiseVelocity;
        private readonly double _measurementNoise;

        private double _distance;
        private double _velocity;
        // Kovariancna matica 2x2 (uchovana ako 4 cisla, aby vyhnut maticovej kniznici,
        // dolezite pre prenositelnost na nanoFramework)
        private double _p00, _p01, _p10, _p11;

        private long _lastTimestampMs = -1;
        private bool _initialized = false;

        public ExtendedKalmanFilter(
            RssiPathLossModel model = null,
            double processNoiseDistance = 0.05,
            double processNoiseVelocity = 0.02,
            double measurementNoise = 4.0)
        {
            _model = model ?? new RssiPathLossModel();
            _processNoiseDistance = processNoiseDistance;
            _processNoiseVelocity = processNoiseVelocity;
            _measurementNoise = measurementNoise;
        }

        public void Reset()
        {
            _initialized = false;
            _lastTimestampMs = -1;
            _distance = 0;
            _velocity = 0;
            _p00 = _p11 = 1.0;
            _p01 = _p10 = 0.0;
        }

        public PositionEstimate Update(RssiSample sample)
        {
            if (!_initialized)
            {
                _distance = _model.EstimateDistance(sample.Rssi);
                _velocity = 0;
                _p00 = 4.0; // pociatocna neistota vzdialenosti
                _p11 = 1.0; // pociatocna neistota rychlosti
                _p01 = _p10 = 0.0;
                _lastTimestampMs = sample.TimestampMs;
                _initialized = true;

                return new PositionEstimate
                {
                    DistanceM = _distance,
                    HeadingDeg = 0,
                    Confidence = 0.3
                };
            }

            double dt = (sample.TimestampMs - _lastTimestampMs) / 1000.0;
            if (dt <= 0) dt = 0.1; // ochrana pri duplicitnych/nezoradenych timestampoch
            _lastTimestampMs = sample.TimestampMs;

            // --- PREDICT ---
            double predictedDistance = _distance + _velocity * dt;
            double predictedVelocity = _velocity;

            // F = [[1, dt], [0, 1]] ; P = F*P*F^T + Q
            double p00 = _p00 + dt * (_p10 + _p01) + dt * dt * _p11 + _processNoiseDistance;
            double p01 = _p01 + dt * _p11;
            double p10 = _p10 + dt * _p11;
            double p11 = _p11 + _processNoiseVelocity;

            // --- UPDATE ---
            double predictedRssi = _model.PredictRssi(predictedDistance);
            double residual = sample.Rssi - predictedRssi;

            double h = _model.MeasurementJacobian(predictedDistance); // H = [h, 0]
            double s = h * p00 * h + _measurementNoise; // S = H*P*H^T + R

            double k0 = p00 * h / s; // Kalmanov zisk pre vzdialenost
            double k1 = p10 * h / s; // Kalmanov zisk pre rychlost

            _distance = predictedDistance + k0 * residual;
            _velocity = predictedVelocity + k1 * residual;

            // P = (I - K*H) * P
            _p00 = (1 - k0 * h) * p00;
            _p01 = (1 - k0 * h) * p01;
            _p10 = p10 - k1 * h * p00;
            _p11 = p11 - k1 * h * p01;

            if (_distance < 0) _distance = 0;

            double confidence = 1.0 / (1.0 + _p00); // hruby odhad na zaklade neistoty

            return new PositionEstimate
            {
                DistanceM = _distance,
                HeadingDeg = 0, // smer sa pocita az vo vyssej vrstve (gradient v case)
                Confidence = confidence
            };
        }
    }
}