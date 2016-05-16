//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public class ShiftedArray<T>
    {
        private T[] Values;

        private int _MinIndex;
        public int MinIndex {
            get {
                return _MinIndex;
            }
        }

        private int _MaxIndex;
        public int MaxIndex {
            get {
                return _MaxIndex;
            }
        }

        public T this [int index] {
            get {
                if (OutOfRange (index)) {
                    throw new IndexOutOfRangeException ();
                }
                return Values [index - MinIndex];
            }
            set {
                if (OutOfRange (index)) {
                    throw new IndexOutOfRangeException ();
                }
                Values [index - MinIndex] = value;
            }
        }

        public bool OutOfRange (int index)
        {
            return ((MaxIndex < index) || (MinIndex > index));
        }

        public ShiftedArray (int minIndex, int maxIndex)
        {
            NcAssert.True (minIndex <= maxIndex);
            _MinIndex = minIndex;
            _MaxIndex = maxIndex;
            Values = new T[maxIndex - minIndex + 1];
        }
    }

    public class NcDistribution
    {
        private int _BinSize;
        public int BinSize {
            get {
                return _BinSize;
            }
        }

        private int _MinBin;
        public int MinBin {
            get {
                return _MinBin;
            }
        }

        private int _MaxBin;
        public int MaxBin {
            get {
                return _MaxBin;
            }
        }

        private uint _TotalSamples;

        private ShiftedArray<uint> _Pdf;
        private ShiftedArray<uint> _Cdf;

        /// We want to minimize the number of cdf updates. We do this by
        /// delaying the update till it is read. We only need to update
        /// all cdf bins equal to and higher than the affected pdf bin.
        /// This index should be the lowest bin index that is updated.
        private int _DirtyIndex;
        public int DirtyIndex {
            /// This getter is only used for unit test.
            get {
                return _DirtyIndex;
            }
        }

        private int ToBin (int sample)
        {
            int bin;
            if (0 <= sample) {
                bin = sample / BinSize;
            } else {
                // Round to -Inf
                bin = (sample - (BinSize - 1)) / BinSize;
            }
            if (bin < MinBin) {
                bin = MinBin;
            } else if (bin > MaxBin) {
                bin = MaxBin;
            }
            return bin;
        }

        public NcDistribution (int binSize, int minBin, int maxBin)
        {
            NcAssert.True ((0 < binSize) && (minBin < maxBin));
            _BinSize = binSize;
            _MinBin = minBin;
            _MaxBin = maxBin;
            _TotalSamples = 0;
            _DirtyIndex = _MaxBin + 1;
            _Pdf = new ShiftedArray<uint> (MinBin, MaxBin);
            _Cdf = new ShiftedArray<uint> (MinBin, MaxBin);
        }

        /// Loads a bin with a count. This method is used for restoring
        /// a distribution from db after a restart.
        public void LoadBin (int bin, uint count)
        {
            if (_Pdf.OutOfRange (bin)) {
                throw new IndexOutOfRangeException ();
            }
            _Pdf [bin] = count;
            _TotalSamples += count;
            if (bin < _DirtyIndex) {
                _DirtyIndex = bin;
            }
        }

        public void AddSample (int sample)
        {
            int bin = ToBin (sample);
            _Pdf [bin]++;
            _TotalSamples++;
            if (bin < _DirtyIndex) {
                _DirtyIndex = bin;
            }
        }

        private void UpdateCdf (int bin)
        {
            uint total = (MinBin == bin) ? 0 : _Cdf[bin-1];
            for (int n = bin; n <= MaxBin; n++) {
                _Cdf [n] = _Pdf [n] + total;
                total += _Pdf [n];
            }
            _DirtyIndex = MaxBin + 1;
        }

        public double Pdf (int sample)
        {
            int bin = ToBin (sample);
            NcAssert.True (_Pdf [bin] <= _TotalSamples);
            if (0 == _TotalSamples) {
                return 0.0;
            }
            return (double)_Pdf [bin] / (double)_TotalSamples;
        }

        public double Cdf (int sample)
        {
            int bin = ToBin (sample);
            if (bin >= _DirtyIndex) {
                UpdateCdf (_DirtyIndex);
                _DirtyIndex = MaxBin + 1;
            }
            NcAssert.True (_Cdf [bin] <= _TotalSamples);
            if (0 == _TotalSamples) {
                return 0.0;
            }
            return (double)_Cdf [bin] / (double)_TotalSamples;
        }
    }
}

