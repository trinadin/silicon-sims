using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FSO.Common.MeshSimplify
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MSTriangleIndices
    {
        public int i0;
        public int i1;
        public int i2;

        public MSTriangleIndices(int i0, int i1, int i2)
        {
            this.i0 = i0; this.i1 = i1; this.i2 = i2;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSTriangleError
    {
        public double e0;
        public double e1;
        public double e2;
        public double e3;
    }

    public static class MSTriangleExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref int GetRef(this ref MSTriangleIndices foo, int i)
        {
            if (i < 0 || i > 2)
            {
                throw new IndexOutOfRangeException();
            }

            return ref Unsafe.Add(ref foo.i0, i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref double GetRef(this ref MSTriangleError foo, int i)
        {
            if (i < 0 || i > 3)
            {
                throw new IndexOutOfRangeException();
            }

            return ref Unsafe.Add(ref foo.e0, i);
        }
    }

    public struct MSTriangle
    {
        public MSTriangleIndices v;
        public MSTriangleError err;
        public bool deleted, dirty;
        public Vector3 n;
    }
}
