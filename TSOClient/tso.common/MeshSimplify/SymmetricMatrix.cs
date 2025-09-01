using System;
using System.Runtime.CompilerServices;

namespace FSO.Common.MeshSimplify
{
    public struct SymmetricMatrix
    {
        public double m11;
        public double m12;
        public double m13;
        public double m14;
        public double m22;
        public double m23;
        public double m24;
        public double m33;
        public double m34;
        public double m44;

        public SymmetricMatrix(double c) : this(c, c, c, c, c, c, c, c, c, c) {
        }


        public SymmetricMatrix(double m11, double m12, double m13, double m14,
                                           double m22, double m23, double m24,
                                                       double m33, double m34,
                                                                   double m44)
        {
            this.m11 = m11; this.m12 = m12; this.m13 = m13; this.m14 = m14;
                            this.m22 = m22; this.m23 = m23; this.m24 = m24;
                                            this.m33 = m33; this.m34 = m34;
                                                            this.m44 = m44;
        }

        // Make plane

        public SymmetricMatrix(double a, double b, double c, double d)
        {
            this.m11 = a * a; this.m12 = a * b; this.m13 = a * c; this.m14 = a * d;
                              this.m22 = b * b; this.m23 = b * c; this.m24 = b * d;
                                                this.m33 = c * c; this.m34 = c * d;
                                                                  this.m44 = d * d;
        }

        public double this[int c] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (c < 0 || c > 9)
                {
                    throw new IndexOutOfRangeException();
                }

                return Unsafe.Add(ref m11, c);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (c < 0 || c > 9)
                {
                    throw new IndexOutOfRangeException();
                }

                Unsafe.Add(ref m11, c) = value;
            }
        }

        //determinant
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double det(int a11, int a12, int a13,
                   int a21, int a22, int a23,
                   int a31, int a32, int a33)
        {
            double det = this[a11] * this[a22] * this[a33] + this[a13] * this[a21] * this[a32] + this[a12] * this[a23] * this[a31]
                        - this[a13] * this[a22] * this[a31] - this[a11] * this[a23] * this[a32] - this[a12] * this[a21] * this[a33];
            return det;
        }

        public static SymmetricMatrix operator +(SymmetricMatrix m, SymmetricMatrix n)
        {
            return new SymmetricMatrix(m.m11 + n.m11, m.m12 + n.m12, m.m13 + n.m13, m.m14 + n.m14,
                                                      m.m22 + n.m22, m.m23 + n.m23, m.m24 + n.m24,
                                                                     m.m33 + n.m33, m.m34 + n.m34,
                                                                                    m.m44 + n.m44);
        }
    }
}
