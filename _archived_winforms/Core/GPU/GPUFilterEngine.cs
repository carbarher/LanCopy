using System;
using System.Collections.Generic;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;
using SlskDown.Models;

namespace SlskDown.Core.GPU
{
    public class GPUFilterEngine : IDisposable
    {
        private readonly Context _context;
        private readonly Accelerator _accelerator;

        public GPUFilterEngine()
        {
            _context = Context.Create(builder => builder.Default());
            _accelerator = _context.GetPreferredDevice(false).CreateAccelerator(_context);
        }

        public static void FilterKernel(
            Index1D index,
            ArrayView<float> bitrates,
            ArrayView<long> sizes,
            ArrayView<int> results)
        {
            bool match = bitrates[index] >= 128 && sizes[index] > 1024 * 1024;
            results[index] = match ? 1 : 0;
        }

        public List<SlskDown.SearchResult> FilterResults(List<SlskDown.SearchResult> results)
        {
            int count = results.Count;
            if (count == 0) return results;

            using var bitrates = _accelerator.Allocate1D<float>(count);
            using var sizes = _accelerator.Allocate1D<long>(count);
            using var output = _accelerator.Allocate1D<int>(count);

            bitrates.CopyFromCPU(results.Select(r => (float)(r.Bitrate ?? 0)).ToArray());
            sizes.CopyFromCPU(results.Select(r => r.Size).ToArray());

            var kernel = _accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView<float>, ArrayView<long>, ArrayView<int>>(FilterKernel);

            kernel(count, bitrates.View, sizes.View, output.View);
            _accelerator.Synchronize();

            var resultArray = output.GetAsArray1D();
            var filtered = new List<SlskDown.SearchResult>();
            for (int i = 0; i < count; i++)
            {
                if (resultArray[i] == 1) filtered.Add(results[i]);
            }
            return filtered;
        }

        public void Dispose()
        {
            _accelerator.Dispose();
            _context.Dispose();
        }
    }
}
