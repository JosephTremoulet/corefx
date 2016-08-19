// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Numerics
{
    /* Note: The following patterns are used throughout the code here and are described here
    *
    * PATTERN:
    *    if (typeof(T) == typeof(Int32)) { ... }
    *    else if (typeof(T) == typeof(Single)) { ... }
    * EXPLANATION:
    *    At runtime, each instantiation of Vector<T> will be type-specific, and each of these typeof blocks will be eliminated,
    *    as typeof(T) is a (JIT) compile-time constant for each instantiation. This design was chosen to eliminate any overhead from
    *    delegates and other patterns.
    *
    * PATTERN:
    *    if (Vector.IsHardwareAccelerated) { ... }
    *    else { ... }
    * EXPLANATION
    *    This pattern solves two problems:
    *        1. Allows us to unroll loops when we know the size (when no hardware acceleration is present)
    *        2. Allows reflection to work:
    *            - If a method is called via reflection, it will not be "intrinsified", which would cause issues if we did
    *              not provide an implementation for that case (i.e. if it only included a case which assumed 16-byte registers)
    *    (NOTE: It is assumed that Vector.IsHardwareAccelerated will be a compile-time constant, eliminating these checks
    *        from the JIT'd code.)
    *
    * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

    /// <summary>
    /// A structure that represents a single Vector. The count of this Vector is fixed but CPU register dependent.
    /// This struct only supports numerical types. This type is intended to be used as a building block for vectorizing
    /// large algorithms. This type is immutable, individual elements cannot be modified.
    /// </summary>
    public struct Vector<T> : IEquatable<Vector<T>>, IFormattable where T : struct
    {
        #region Fields
        private ulong _ulong;
        private double _double;
        #endregion Fields

        private Vector(double d, bool dummy) : this()
        {
            _double = d;
        }

        private Vector(ulong u, bool dummy) : this()
        {
            _ulong = u;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void throwNotSupported()
        {
            throw new NotSupportedException(SR.Arg_TypeNotSupported);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void throwNotYetImplemented()
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void throwNullRef()
        {
            // Match the JIT's exception type here. For perf, a NullReference is thrown instead of an ArgumentNull.
            throw new NullReferenceException(SR.Arg_NullArgumentNullRef);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void throwIndex()
        {
            throw new IndexOutOfRangeException();
        }

        #region Static Members
        /// <summary>
        /// Returns the number of elements stored in the vector. This value is hardware dependent.
        /// </summary>
        [JitIntrinsic]
        public static int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (typeof(T) == typeof(Byte))
                {
                    return 8;
                }
                else if (typeof(T) == typeof(SByte))
                {
                    return 8;
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    return 4;
                }
                else if (typeof(T) == typeof(Int16))
                {
                    return 4;
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    return 2;
                }
                else if (typeof(T) == typeof(Int32))
                {
                    return 2;
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    return 1;
                }
                else if (typeof(T) == typeof(Int64))
                {
                    return 1;
                }
                else if (typeof(T) == typeof(Single))
                {
                    return 2;
                }
                else
                {
                    if (typeof(T) != typeof(Double))
                    {
                        throwNotSupported();
                    }
                    return 1;
                }
            }
        }
        // private static readonly int s_count = InitializeCount();

        /// <summary>
        /// Returns a vector containing all zeroes.
        /// </summary>
        [JitIntrinsic]
        public static Vector<T> Zero { [MethodImpl(MethodImplOptions.AggressiveInlining)]get { return new Vector<T>(GetZeroValue()); } }
        // private static readonly Vector<T> zero = new Vector<T>(GetZeroValue());

        /// <summary>
        /// Returns a vector containing all ones.
        /// </summary>
        [JitIntrinsic]
        public static Vector<T> One { [MethodImpl(MethodImplOptions.AggressiveInlining)]get { return new Vector<T>(GetOneValue()); } }
        // private static readonly Vector<T> one = new Vector<T>(GetOneValue());

        internal static Vector<T> AllOnes { [MethodImpl(MethodImplOptions.AggressiveInlining)]get { return new Vector<T>(GetAllBitsSetValue()); } }
        // private static readonly Vector<T> allOnes = new Vector<T>(GetAllBitsSetValue());
        #endregion Static Members

        #region Constructors
        /// <summary>
        /// Constructs a vector whose components are all <code>value</code>
        /// </summary>
        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Vector(T value)
            : this()
        {
            if (Vector.IsHardwareAccelerated)
            {
                throwNotYetImplemented();
            }
            else
            {
                uint one, two, four;

                if (typeof(T) == typeof(Byte))
                {
                    one = (uint)(Byte)(object)value;
                    two = one | (one << 8);
                    four = two | (two << 16);
                    _ulong = ((ulong)four) | ((ulong)four << 32);
                }
                else if (typeof(T) == typeof(SByte))
                {
                    one = (uint)(Byte)(SByte)(object)value;
                    two = one | (one << 8);
                    four = two | (two << 16);
                    _ulong = ((ulong)four) | ((ulong)four << 32);
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    two = (uint)(UInt16)(object)value;
                    four = two | (two << 16);
                    _ulong = ((ulong)four) | ((ulong)four << 32);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    two = (uint)(UInt16)(Int16)(object)value;
                    four = two | (two << 16);
                    _ulong = ((ulong)four) | ((ulong)four << 32);
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    four = (uint)(UInt32)(object)value;
                    _ulong = ((ulong)four) | ((ulong)four << 32);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    four = (uint)(UInt32)(Int32)(object)value;
                    _ulong = ((ulong)four) | ((ulong)four << 32);
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    _ulong = (UInt64)(object)value;
                }
                else if (typeof(T) == typeof(Int64))
                {
                    _ulong = (ulong)(Int64)(object)value;
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                }
                else if (typeof(T) == typeof(Double))
                {
                    _double = (Double)(object)value;
                }
            }
        }

        /// <summary>
        /// Constructs a vector from the given array. The size of the given array must be at least Vector'T.Count.
        /// </summary>
        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Vector(T[] values) : this(values, 0) { }

        private static unsafe ulong readUlong(T[] values, int index)
        {
            if (typeof(T) == typeof(Byte))
            {
                fixed (Byte* ptr = &(values as Byte[])[index])
                {
                    return *((ulong*)ptr);
                }
            }
            else if (typeof(T) == typeof(SByte))
            {
                fixed (SByte* ptr = &(values as SByte[])[index])
                {
                    return *((ulong*)ptr);
                }
            }
            else if (typeof(T) == typeof(UInt16))
            {
                fixed (UInt16* ptr = &(values as UInt16[])[index])
                {
                    return *((ulong*)ptr);
                }
            }
            else if (typeof(T) == typeof(Int16))
            {
                fixed (Int16* ptr = &(values as Int16[])[index])
                {
                    return *((ulong*)ptr);
                }
            }
            else if (typeof(T) == typeof(UInt32))
            {
                fixed (UInt32* ptr = &(values as UInt32[])[index])
                {
                    return *((ulong*)ptr);
                }
            }
            else if (typeof(T) == typeof(Int32))
            {
                fixed (Int32* ptr = &(values as Int32[])[index])
                {
                    return *((ulong*)ptr);
                }
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return (values as UInt64[])[index];
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (ulong)((values as Int64[])[index]);
            }
            else
            {
                if (typeof(T) != typeof(Single))
                {
                    throwNotSupported();
                }
                fixed (Single* ptr = &(values as Single[])[index])
                {
                    return *((ulong*)ptr);
                }
            }
        }

        /// <summary>
        /// Constructs a vector from the given array, starting from the given index.
        /// The array must contain at least Vector'T.Count from the given index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Vector(T[] values, int index)
            : this()
        {
            if (values == null)
            {
                throwNullRef();
            }
            if (index < 0 || (values.Length - index) < Count)
            {
                throwIndex();
            }

            if (Vector.IsHardwareAccelerated)
            {
                throwNotYetImplemented();
            }
            else
            {
                if (typeof(T) == typeof(Double))
                {
                    _double = (values as Double[])[index];
                }
                else
                {
                    _ulong = readUlong(values, index);
                }
            }
        }

#pragma warning disable 3001 // void* is not a CLS-Compliant argument type
        private unsafe Vector(void* dataPointer) : this(dataPointer, 0) { }
#pragma warning restore 3001 // void* is not a CLS-Compliant argument type

#pragma warning disable 3001 // void* is not a CLS-Compliant argument type
        // Implemented with offset if this API ever becomes public; an offset of 0 is used internally.
        private unsafe Vector(void* dataPointer, int offset)
            : this()
        {
            if (typeof(T) == typeof(Byte))
            {
                Byte* castedPtr = (Byte*)dataPointer;
                castedPtr += offset;
                _ulong = *((ulong*)castedPtr);
            }
            else if (typeof(T) == typeof(SByte))
            {
                SByte* castedPtr = (SByte*)dataPointer;
                castedPtr += offset;
                _ulong = *((ulong*)castedPtr);
            }
            else if (typeof(T) == typeof(UInt16))
            {
                UInt16* castedPtr = (UInt16*)dataPointer;
                castedPtr += offset;
                _ulong = *((ulong*)castedPtr);
            }
            else if (typeof(T) == typeof(Int16))
            {
                Int16* castedPtr = (Int16*)dataPointer;
                castedPtr += offset;
                _ulong = *((ulong*)castedPtr);
            }
            else if (typeof(T) == typeof(UInt32))
            {
                UInt32* castedPtr = (UInt32*)dataPointer;
                castedPtr += offset;
                _ulong = *((ulong*)castedPtr);
            }
            else if (typeof(T) == typeof(Int32))
            {
                Int32* castedPtr = (Int32*)dataPointer;
                castedPtr += offset;
                _ulong = *((ulong*)castedPtr);
            }
            else if (typeof(T) == typeof(UInt64))
            {
                UInt64* castedPtr = (UInt64*)dataPointer;
                castedPtr += offset;
                _ulong = *((ulong*)castedPtr);
            }
            else if (typeof(T) == typeof(Int64))
            {
                Int64* castedPtr = (Int64*)dataPointer;
                castedPtr += offset;
                _ulong = *((ulong*)castedPtr);
            }
            else if (typeof(T) == typeof(Single))
            {
                Single* castedPtr = (Single*)dataPointer;
                castedPtr += offset;
                _ulong = *((ulong*)castedPtr);
            }
            else if (typeof(T) == typeof(Double))
            {
                Double* castedPtr = (Double*)dataPointer;
                castedPtr += offset;
                _double = *((double*)castedPtr);
            }
            else
            {
                // throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }
#pragma warning restore 3001 // void* is not a CLS-Compliant argument type

        #endregion Constructors

        #region Public Instance Methods
        /// <summary>
        /// Copies the vector to the given destination array. The destination array must be at least size Vector'T.Count.
        /// </summary>
        /// <param name="destination">The destination array which the values are copied into</param>
        /// <exception cref="ArgumentNullException">If the destination array is null</exception>
        /// <exception cref="ArgumentException">If number of elements in source vector is greater than those available in destination array</exception>
        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(T[] destination)
        {
            CopyTo(destination, 0);
        }

        /// <summary>
        /// Copies the vector to the given destination array. The destination array must be at least size Vector'T.Count.
        /// </summary>
        /// <param name="destination">The destination array which the values are copied into</param>
        /// <param name="startIndex">The index to start copying to</param>
        /// <exception cref="ArgumentNullException">If the destination array is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">If index is greater than end of the array or index is less than zero</exception>
        /// <exception cref="ArgumentException">If number of elements in source vector is greater than those available in destination array</exception>
        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(T[] destination, int startIndex)
        {
            if (destination == null)
            {
                // Match the JIT's exception type here. For perf, a NullReference is thrown instead of an ArgumentNull.
                throw new NullReferenceException(SR.Arg_NullArgumentNullRef);
            }
            if (startIndex < 0 || startIndex >= destination.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.Format(SR.Arg_ArgumentOutOfRangeException, startIndex));
            }
            if ((destination.Length - startIndex) < Count)
            {
                throw new ArgumentException(SR.Format(SR.Arg_ElementsInSourceIsGreaterThanDestination, startIndex));
            }

            if (Vector.IsHardwareAccelerated)
            {
                throw new System.Exception("NYI");
            }
            else
            {
                if (typeof(T) == typeof(Byte))
                {
                    fixed(Byte* destPtr = &(destination as Byte[])[startIndex])
                    {
                        *((ulong*)destPtr) = _ulong;
                    }
                }
                else if (typeof(T) == typeof(SByte))
                {
                    fixed (SByte* destPtr = &(destination as SByte[])[startIndex])
                    {
                        *((ulong*)destPtr) = _ulong;
                    }
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    fixed (UInt16* destPtr = &(destination as UInt16[])[startIndex])
                    {
                        *((ulong*)destPtr) = _ulong;
                    }
                }
                else if (typeof(T) == typeof(Int16))
                {
                    fixed (Int16* destPtr = &(destination as Int16[])[startIndex])
                    {
                        *((ulong*)destPtr) = _ulong;
                    }
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    fixed (UInt32* destPtr = &(destination as UInt32[])[startIndex])
                    {
                        *((ulong*)destPtr) = _ulong;
                    }
                }
                else if (typeof(T) == typeof(Int32))
                {
                    fixed (Int32* destPtr = &(destination as Int32[])[startIndex])
                    {
                        *((ulong*)destPtr) = _ulong;
                    }
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    (destination as UInt64[])[startIndex] = _ulong;
                }
                else if (typeof(T) == typeof(Int64))
                {
                    (destination as Int64[])[startIndex] = (Int64)_ulong;
                }
                else if (typeof(T) == typeof(Single))
                {
                    fixed (Single* destPtr = &(destination as Single[])[startIndex])
                    {
                        *((ulong*)destPtr) = _ulong;
                    }
                }
                else if (typeof(T) == typeof(Double))
                {
                    (destination as Double[])[startIndex] = _double;
                }
                else
                {
                    // throw new System.Exception("TODO: right exception kind");
                }
            }
        }

        /// <summary>
        /// Returns the element at the given index.
        /// </summary>
        [JitIntrinsic]
        public unsafe T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (index >= Count || index < 0)
                {
                    throw new IndexOutOfRangeException(SR.Format(SR.Arg_ArgumentOutOfRangeException, index));
                }
                if (typeof(T) == typeof(Byte))
                {
                    return (T)(object)(byte)(_ulong >> 8 * index);
                }
                else if (typeof(T) == typeof(SByte))
                {
                    return (T)(object)(sbyte)(_ulong >> 8 * index);
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    return (T)(object)(ushort)(_ulong >> 16 * index);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    return (T)(object)(short)(_ulong >> 16 * index);
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    return (T)(object)(uint)(_ulong >> 32 * index);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    return (T)(object)(int)(_ulong >> 32 * index);
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    return (T)(object)_ulong;
                }
                else if (typeof(T) == typeof(Int64))
                {
                    return (T)(object)(long)_ulong;
                }
                else if (typeof(T) == typeof(Single))
                {
                    return (T)(object)(Single)0.0;// throw new System.Exception("Need BitConverter 32-bit ops");
                }
                else 
                {
                    if (typeof(T) != typeof(Double))
                    {
                        throwNotSupported();
                    }
                    return (T)(object)_double;
                }
            }
        }

        /// <summary>
        /// Returns a boolean indicating whether the given Object is equal to this vector instance.
        /// </summary>
        /// <param name="obj">The Object to compare against.</param>
        /// <returns>True if the Object is equal to this vector; False otherwise.</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (!(obj is Vector<T>))
            {
                return false;
            }
            return Equals((Vector<T>)obj);
        }

        /// <summary>
        /// Returns a boolean indicating whether the given vector is equal to this vector instance.
        /// </summary>
        /// <param name="other">The vector to compare this instance to.</param>
        /// <returns>True if the other vector is equal to this instance; False otherwise.</returns>
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Vector<T> other)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return Equals(other);
            }
            else
            {
                if (typeof(T) == typeof(Double))
                {
                    return _double == other._double;
                }

                return _ulong == other._ulong;
            }
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            if (Vector.IsHardwareAccelerated)
            {
                return GetHashCode();
            }
            else
            {
                if (typeof(T) == typeof(Double))
                {
                    return _double.GetHashCode();
                }
                return _ulong.GetHashCode();
            }
        }

        /// <summary>
        /// Returns a String representing this vector.
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString()
        {
            return ToString("G", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Returns a String representing this vector, using the specified format string to format individual elements.
        /// </summary>
        /// <param name="format">The format of individual elements.</param>
        /// <returns>The string representation.</returns>
        public string ToString(string format)
        {
            return ToString(format, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Returns a String representing this vector, using the specified format string to format individual elements
        /// and the given IFormatProvider.
        /// </summary>
        /// <param name="format">The format of individual elements.</param>
        /// <param name="formatProvider">The format provider to use when formatting elements.</param>
        /// <returns>The string representation.</returns>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            StringBuilder sb = new StringBuilder();
            string separator = NumberFormatInfo.GetInstance(formatProvider).NumberGroupSeparator;
            sb.Append('<');
            for (int g = 0; g < Count - 1; g++)
            {
                sb.Append(((IFormattable)this[g]).ToString(format, formatProvider));
                sb.Append(separator);
                sb.Append(' ');
            }
            // Append last element w/out separator
            sb.Append(((IFormattable)this[Count - 1]).ToString(format, formatProvider));
            sb.Append('>');
            return sb.ToString();
        }
        #endregion Public Instance Methods

        #region Arithmetic Operators
        /// <summary>
        /// Adds two vectors together.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The summed vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> operator +(Vector<T> left, Vector<T> right)
        {
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    return left + right;
                }
                else
                {
                    ulong left_ulong;
                    ulong right_ulong;
                    if (typeof(T) == typeof(Byte))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>( (((left_ulong & 0x00000000000000ffUL) + (right_ulong & 0x00000000000000ffUL)) & 0x00000000000000ffUL)
                                   | (((left_ulong & 0x000000000000ff00UL) + (right_ulong & 0x000000000000ff00UL)) & 0x000000000000ff00UL)
                                   | (((left_ulong & 0x0000000000ff0000UL) + (right_ulong & 0x0000000000ff0000UL)) & 0x0000000000ff0000UL)
                                   | (((left_ulong & 0x00000000ff000000UL) + (right_ulong & 0x00000000ff000000UL)) & 0x00000000ff000000UL)
                                   | (((left_ulong & 0x000000ff00000000UL) + (right_ulong & 0x000000ff00000000UL)) & 0x000000ff00000000UL)
                                   | (((left_ulong & 0x0000ff0000000000UL) + (right_ulong & 0x0000ff0000000000UL)) & 0x0000ff0000000000UL)
                                   | (((left_ulong & 0x00ff000000000000UL) + (right_ulong & 0x00ff000000000000UL)) & 0x00ff000000000000UL)
                                   | (((left_ulong & 0xff00000000000000UL) + (right_ulong & 0xff00000000000000UL)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(SByte))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000000000ffUL) + (right_ulong & 0x00000000000000ffUL)) & 0x00000000000000ffUL)
                                   | (((left_ulong & 0x000000000000ff00UL) + (right_ulong & 0x000000000000ff00UL)) & 0x000000000000ff00UL)
                                   | (((left_ulong & 0x0000000000ff0000UL) + (right_ulong & 0x0000000000ff0000UL)) & 0x0000000000ff0000UL)
                                   | (((left_ulong & 0x00000000ff000000UL) + (right_ulong & 0x00000000ff000000UL)) & 0x00000000ff000000UL)
                                   | (((left_ulong & 0x000000ff00000000UL) + (right_ulong & 0x000000ff00000000UL)) & 0x000000ff00000000UL)
                                   | (((left_ulong & 0x0000ff0000000000UL) + (right_ulong & 0x0000ff0000000000UL)) & 0x0000ff0000000000UL)
                                   | (((left_ulong & 0x00ff000000000000UL) + (right_ulong & 0x00ff000000000000UL)) & 0x00ff000000000000UL)
                                   | (((left_ulong & 0xff00000000000000UL) + (right_ulong & 0xff00000000000000UL)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt16))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x000000000000ffffUL) + (right_ulong & 0x000000000000ffffUL)) & 0x000000000000ffffUL)
                                   | (((left_ulong & 0x00000000ffff0000UL) + (right_ulong & 0x00000000ffff0000UL)) & 0x00000000ffff0000UL)
                                   | (((left_ulong & 0x0000ffff00000000UL) + (right_ulong & 0x0000ffff00000000UL)) & 0x0000ffff00000000UL)
                                   | (((left_ulong & 0xffff000000000000UL) + (right_ulong & 0xffff000000000000UL)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int16))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x000000000000ffffUL) + (right_ulong & 0x000000000000ffffUL)) & 0x000000000000ffffUL)
                                   | (((left_ulong & 0x00000000ffff0000UL) + (right_ulong & 0x00000000ffff0000UL)) & 0x00000000ffff0000UL)
                                   | (((left_ulong & 0x0000ffff00000000UL) + (right_ulong & 0x0000ffff00000000UL)) & 0x0000ffff00000000UL)
                                   | (((left_ulong & 0xffff000000000000UL) + (right_ulong & 0xffff000000000000UL)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt32))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000ffffffffUL) + (right_ulong & 0x00000000ffffffffUL)) & 0x00000000ffffffffUL)
                                   | (((left_ulong & 0xffffffff00000000UL) + (right_ulong & 0xffffffff00000000UL)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int32))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000ffffffffUL) + (right_ulong & 0x00000000ffffffffUL)) & 0x00000000ffffffffUL)
                                   | (((left_ulong & 0xffffffff00000000UL) + (right_ulong & 0xffffffff00000000UL)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt64))
                    {
                        return new Vector<T>(left._ulong + right._ulong, true);
                    }
                    else if (typeof(T) == typeof(Int64))
                    {
                        return new Vector<T>(left._ulong + right._ulong, true);
                    }
                    else if (typeof(T) == typeof(Single))
                    {
                        throwNotYetImplemented();
                        return new Vector<T>();
                    }
                    else if (typeof(T) == typeof(Double))
                    {
                        return new Vector<T>(left._double + right._double, true);
                    }
                    return new Vector<T>();
                }
            }
        }

        /// <summary>
        /// Subtracts the second vector from the first.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The difference vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> operator -(Vector<T> left, Vector<T> right)
        {
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    return left - right;
                }
                else
                {
                    ulong left_ulong;
                    ulong right_ulong;
                    if (typeof(T) == typeof(Byte))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000000000ffUL) - (right_ulong & 0x00000000000000ffUL)) & 0x00000000000000ffUL)
                                          | (((left_ulong & 0x000000000000ff00UL) - (right_ulong & 0x000000000000ff00UL)) & 0x000000000000ff00UL)
                                          | (((left_ulong & 0x0000000000ff0000UL) - (right_ulong & 0x0000000000ff0000UL)) & 0x0000000000ff0000UL)
                                          | (((left_ulong & 0x00000000ff000000UL) - (right_ulong & 0x00000000ff000000UL)) & 0x00000000ff000000UL)
                                          | (((left_ulong & 0x000000ff00000000UL) - (right_ulong & 0x000000ff00000000UL)) & 0x000000ff00000000UL)
                                          | (((left_ulong & 0x0000ff0000000000UL) - (right_ulong & 0x0000ff0000000000UL)) & 0x0000ff0000000000UL)
                                          | (((left_ulong & 0x00ff000000000000UL) - (right_ulong & 0x00ff000000000000UL)) & 0x00ff000000000000UL)
                                          | (((left_ulong & 0xff00000000000000UL) - (right_ulong & 0xff00000000000000UL)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(SByte))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000000000ffUL) - (right_ulong & 0x00000000000000ffUL)) & 0x00000000000000ffUL)
                                          | (((left_ulong & 0x000000000000ff00UL) - (right_ulong & 0x000000000000ff00UL)) & 0x000000000000ff00UL)
                                          | (((left_ulong & 0x0000000000ff0000UL) - (right_ulong & 0x0000000000ff0000UL)) & 0x0000000000ff0000UL)
                                          | (((left_ulong & 0x00000000ff000000UL) - (right_ulong & 0x00000000ff000000UL)) & 0x00000000ff000000UL)
                                          | (((left_ulong & 0x000000ff00000000UL) - (right_ulong & 0x000000ff00000000UL)) & 0x000000ff00000000UL)
                                          | (((left_ulong & 0x0000ff0000000000UL) - (right_ulong & 0x0000ff0000000000UL)) & 0x0000ff0000000000UL)
                                          | (((left_ulong & 0x00ff000000000000UL) - (right_ulong & 0x00ff000000000000UL)) & 0x00ff000000000000UL)
                                          | (((left_ulong & 0xff00000000000000UL) - (right_ulong & 0xff00000000000000UL)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt16))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x000000000000ffffUL) - (right_ulong & 0x000000000000ffffUL)) & 0x000000000000ffffUL)
                                          | (((left_ulong & 0x00000000ffff0000UL) - (right_ulong & 0x00000000ffff0000UL)) & 0x00000000ffff0000UL)
                                          | (((left_ulong & 0x0000ffff00000000UL) - (right_ulong & 0x0000ffff00000000UL)) & 0x0000ffff00000000UL)
                                          | (((left_ulong & 0xffff000000000000UL) - (right_ulong & 0xffff000000000000UL)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int16))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x000000000000ffffUL) - (right_ulong & 0x000000000000ffffUL)) & 0x000000000000ffffUL)
                                          | (((left_ulong & 0x00000000ffff0000UL) - (right_ulong & 0x00000000ffff0000UL)) & 0x00000000ffff0000UL)
                                          | (((left_ulong & 0x0000ffff00000000UL) - (right_ulong & 0x0000ffff00000000UL)) & 0x0000ffff00000000UL)
                                          | (((left_ulong & 0xffff000000000000UL) - (right_ulong & 0xffff000000000000UL)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt32))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000ffffffffUL) - (right_ulong & 0x00000000ffffffffUL)) & 0x00000000ffffffffUL)
                                          | (((left_ulong & 0xffffffff00000000UL) - (right_ulong & 0xffffffff00000000UL)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int32))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000ffffffffUL) - (right_ulong & 0x00000000ffffffffUL)) & 0x00000000ffffffffUL)
                                          | (((left_ulong & 0xffffffff00000000UL) - (right_ulong & 0xffffffff00000000UL)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt64))
                    {
                        return new Vector<T>(left._ulong - right._ulong, true);
                    }
                    else if (typeof(T) == typeof(Int64))
                    {
                        return new Vector<T>(left._ulong - right._ulong, true);
                    }
                    else if (typeof(T) == typeof(Single))
                    {
                        throwNotYetImplemented();
                        return new Vector<T>();
                    }
                    else if (typeof(T) == typeof(Double))
                    {
                        return new Vector<T>(left._double - right._double, true);
                    }
                    return new Vector<T>();
                }
            }
        }

        // This method is intrinsic only for certain types. It cannot access fields directly unless we are sure the context is unaccelerated.
        /// <summary>
        /// Multiplies two vectors together.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The product vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> operator *(Vector<T> left, Vector<T> right)
        {
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    return left * right; //  throw new System.Exception("NYI");
                }
                else
                {
                    ulong left_ulong;
                    ulong right_ulong;
                    if (typeof(T) == typeof(Byte))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000000000ffUL) * (right_ulong & 0x00000000000000ffUL)) & 0x00000000000000ffUL)
                                       | (((left_ulong & 0x000000000000ff00UL) * (right_ulong & 0x000000000000ff00UL)) & 0x000000000000ff00UL)
                                       | (((left_ulong & 0x0000000000ff0000UL) * (right_ulong & 0x0000000000ff0000UL)) & 0x0000000000ff0000UL)
                                       | (((left_ulong & 0x00000000ff000000UL) * (right_ulong & 0x00000000ff000000UL)) & 0x00000000ff000000UL)
                                       | (((left_ulong & 0x000000ff00000000UL) * (right_ulong & 0x000000ff00000000UL)) & 0x000000ff00000000UL)
                                       | (((left_ulong & 0x0000ff0000000000UL) * (right_ulong & 0x0000ff0000000000UL)) & 0x0000ff0000000000UL)
                                       | (((left_ulong & 0x00ff000000000000UL) * (right_ulong & 0x00ff000000000000UL)) & 0x00ff000000000000UL)
                                       | (((left_ulong & 0xff00000000000000UL) * (right_ulong & 0xff00000000000000UL)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(SByte))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000000000ffUL) * (right_ulong & 0x00000000000000ffUL)) & 0x00000000000000ffUL)
                                       | (((left_ulong & 0x000000000000ff00UL) * (right_ulong & 0x000000000000ff00UL)) & 0x000000000000ff00UL)
                                       | (((left_ulong & 0x0000000000ff0000UL) * (right_ulong & 0x0000000000ff0000UL)) & 0x0000000000ff0000UL)
                                       | (((left_ulong & 0x00000000ff000000UL) * (right_ulong & 0x00000000ff000000UL)) & 0x00000000ff000000UL)
                                       | (((left_ulong & 0x000000ff00000000UL) * (right_ulong & 0x000000ff00000000UL)) & 0x000000ff00000000UL)
                                       | (((left_ulong & 0x0000ff0000000000UL) * (right_ulong & 0x0000ff0000000000UL)) & 0x0000ff0000000000UL)
                                       | (((left_ulong & 0x00ff000000000000UL) * (right_ulong & 0x00ff000000000000UL)) & 0x00ff000000000000UL)
                                       | (((left_ulong & 0xff00000000000000UL) * (right_ulong & 0xff00000000000000UL)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt16))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x000000000000ffffUL) * (right_ulong & 0x000000000000ffffUL)) & 0x000000000000ffffUL)
                                       | (((left_ulong & 0x00000000ffff0000UL) * (right_ulong & 0x00000000ffff0000UL)) & 0x00000000ffff0000UL)
                                       | (((left_ulong & 0x0000ffff00000000UL) * (right_ulong & 0x0000ffff00000000UL)) & 0x0000ffff00000000UL)
                                       | (((left_ulong & 0xffff000000000000UL) * (right_ulong & 0xffff000000000000UL)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int16))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x000000000000ffffUL) * (right_ulong & 0x000000000000ffffUL)) & 0x000000000000ffffUL)
                                       | (((left_ulong & 0x00000000ffff0000UL) * (right_ulong & 0x00000000ffff0000UL)) & 0x00000000ffff0000UL)
                                       | (((left_ulong & 0x0000ffff00000000UL) * (right_ulong & 0x0000ffff00000000UL)) & 0x0000ffff00000000UL)
                                       | (((left_ulong & 0xffff000000000000UL) * (right_ulong & 0xffff000000000000UL)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt32))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000ffffffffUL) * (right_ulong & 0x00000000ffffffffUL)) & 0x00000000ffffffffUL)
                                       | (((left_ulong & 0xffffffff00000000UL) * (right_ulong & 0xffffffff00000000UL)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int32))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000ffffffffUL) * (right_ulong & 0x00000000ffffffffUL)) & 0x00000000ffffffffUL)
                                       | (((left_ulong & 0xffffffff00000000UL) * (right_ulong & 0xffffffff00000000UL)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt64))
                    {
                        return new Vector<T>(left._ulong * right._ulong, true);
                    }
                    else if (typeof(T) == typeof(Int64))
                    {
                        return new Vector<T>(left._ulong * right._ulong, true);
                    }
                    else if (typeof(T) == typeof(Single))
                    {
                        throwNotYetImplemented();
                        return new Vector<T>();
                    }
                    else if (typeof(T) == typeof(Double))
                    {
                        return new Vector<T>(left._double * right._double, true);
                    }
                    return new Vector<T>();
                }
            }
        }

        // This method is intrinsic only for certain types. It cannot access fields directly unless we are sure the context is unaccelerated.
        /// <summary>
        /// Multiplies a vector by the given scalar.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <param name="factor">The scalar value.</param>
        /// <returns>The scaled vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector<T> operator *(Vector<T> value, T factor)
        {
            return factor * value;
        }

        // This method is intrinsic only for certain types. It cannot access fields directly unless we are sure the context is unaccelerated.
        /// <summary>
        /// Multiplies a vector by the given scalar.
        /// </summary>
        /// <param name="factor">The scalar value.</param>
        /// <param name="value">The source vector.</param>
        /// <returns>The scaled vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector<T> operator *(T factor, Vector<T> value)
        {
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    return factor * value;
                }
                else
                {
                    ulong value_ulong;
                    ulong factor_ulong;
                    if (typeof(T) == typeof(Byte))
                    {
                        value_ulong = value._ulong;
                        factor_ulong = (ulong)(Byte)(object)factor;
                        return new Vector<T>((((value_ulong & 0x00000000000000ffUL) * (factor_ulong << 00)) & 0x00000000000000ffUL)
                                       | (((value_ulong & 0x000000000000ff00UL) * (factor_ulong << 08)) & 0x000000000000ff00UL)
                                       | (((value_ulong & 0x0000000000ff0000UL) * (factor_ulong << 16)) & 0x0000000000ff0000UL)
                                       | (((value_ulong & 0x00000000ff000000UL) * (factor_ulong << 24)) & 0x00000000ff000000UL)
                                       | (((value_ulong & 0x000000ff00000000UL) * (factor_ulong << 32)) & 0x000000ff00000000UL)
                                       | (((value_ulong & 0x0000ff0000000000UL) * (factor_ulong << 40)) & 0x0000ff0000000000UL)
                                       | (((value_ulong & 0x00ff000000000000UL) * (factor_ulong << 48)) & 0x00ff000000000000UL)
                                       | (((value_ulong & 0xff00000000000000UL) * (factor_ulong << 56)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(SByte))
                    {
                        value_ulong = value._ulong;
                        factor_ulong = (ulong)(byte)(SByte)(object)factor;
                        return new Vector<T>((((value_ulong & 0x00000000000000ffUL) * (factor_ulong << 00)) & 0x00000000000000ffUL)
                                       | (((value_ulong & 0x000000000000ff00UL) * (factor_ulong << 08)) & 0x000000000000ff00UL)
                                       | (((value_ulong & 0x0000000000ff0000UL) * (factor_ulong << 16)) & 0x0000000000ff0000UL)
                                       | (((value_ulong & 0x00000000ff000000UL) * (factor_ulong << 24)) & 0x00000000ff000000UL)
                                       | (((value_ulong & 0x000000ff00000000UL) * (factor_ulong << 32)) & 0x000000ff00000000UL)
                                       | (((value_ulong & 0x0000ff0000000000UL) * (factor_ulong << 40)) & 0x0000ff0000000000UL)
                                       | (((value_ulong & 0x00ff000000000000UL) * (factor_ulong << 48)) & 0x00ff000000000000UL)
                                       | (((value_ulong & 0xff00000000000000UL) * (factor_ulong << 56)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt16))
                    {
                        value_ulong = value._ulong;
                        factor_ulong = (ulong)(UInt16)(object)factor;
                        return new Vector<T>((((value_ulong & 0x000000000000ffffUL) * (factor_ulong << 00)) & 0x000000000000ffffUL)
                                       | (((value_ulong & 0x00000000ffff0000UL) * (factor_ulong << 16)) & 0x00000000ffff0000UL)
                                       | (((value_ulong & 0x0000ffff00000000UL) * (factor_ulong << 32)) & 0x0000ffff00000000UL)
                                       | (((value_ulong & 0xffff000000000000UL) * (factor_ulong << 48)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int16))
                    {
                        value_ulong = value._ulong;
                        factor_ulong = (ulong)(ushort)(Int16)(object)factor;
                        return new Vector<T>((((value_ulong & 0x000000000000ffffUL) * (factor_ulong << 00)) & 0x000000000000ffffUL)
                                       | (((value_ulong & 0x00000000ffff0000UL) * (factor_ulong << 16)) & 0x00000000ffff0000UL)
                                       | (((value_ulong & 0x0000ffff00000000UL) * (factor_ulong << 32)) & 0x0000ffff00000000UL)
                                       | (((value_ulong & 0xffff000000000000UL) * (factor_ulong << 48)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt32))
                    {
                        value_ulong = value._ulong;
                        factor_ulong = (ulong)(UInt32)(object)factor;
                        return new Vector<T>((((value_ulong & 0x000000000000ffffUL) * (factor_ulong << 00)) & 0x00000000ffffffffUL)
                                       | (((value_ulong & 0x0000ffff00000000UL) * (factor_ulong << 32)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int32))
                    {
                        value_ulong = value._ulong;
                        factor_ulong = (ulong)(uint)(Int32)(object)factor;
                        return new Vector<T>((((value_ulong & 0x000000000000ffffUL) * (factor_ulong << 00)) & 0x00000000ffffffffUL)
                                       | (((value_ulong & 0x0000ffff00000000UL) * (factor_ulong << 32)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt64))
                    {
                        return new Vector<T>(value._ulong * (UInt64)(object)factor, true);
                    }
                    else if (typeof(T) == typeof(Int64))
                    {
                        return new Vector<T>(value._ulong * (ulong)(Int64)(object)factor, true);
                    }
                    else if (typeof(T) == typeof(Single))
                    {
                        throwNotYetImplemented();
                        return new Vector<T>();
                    }
                    else if (typeof(T) == typeof(Double))
                    {
                        return new Vector<T>(value._double * (Double)(object)factor, true);
                    }
                    return new Vector<T>();
                }
            }
        }

        // This method is intrinsic only for certain types. It cannot access fields directly unless we are sure the context is unaccelerated.
        /// <summary>
        /// Divides the first vector by the second.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The vector resulting from the division.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> operator /(Vector<T> left, Vector<T> right)
        {
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    return left / right;
                }
                else
                {
                    ulong left_ulong;
                    ulong right_ulong;
                    if (typeof(T) == typeof(Byte))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000000000ffUL) * (right_ulong & 0x00000000000000ffUL)) & 0x00000000000000ffUL)
                                        | (((left_ulong & 0x000000000000ff00UL) * (right_ulong & 0x000000000000ff00UL)) & 0x000000000000ff00UL)
                                        | (((left_ulong & 0x0000000000ff0000UL) * (right_ulong & 0x0000000000ff0000UL)) & 0x0000000000ff0000UL)
                                        | (((left_ulong & 0x00000000ff000000UL) * (right_ulong & 0x00000000ff000000UL)) & 0x00000000ff000000UL)
                                        | (((left_ulong & 0x000000ff00000000UL) * (right_ulong & 0x000000ff00000000UL)) & 0x000000ff00000000UL)
                                        | (((left_ulong & 0x0000ff0000000000UL) * (right_ulong & 0x0000ff0000000000UL)) & 0x0000ff0000000000UL)
                                        | (((left_ulong & 0x00ff000000000000UL) * (right_ulong & 0x00ff000000000000UL)) & 0x00ff000000000000UL)
                                        | (((left_ulong & 0xff00000000000000UL) * (right_ulong & 0xff00000000000000UL)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(SByte))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000000000ffUL) * (right_ulong & 0x00000000000000ffUL)) & 0x00000000000000ffUL)
                                        | (((left_ulong & 0x000000000000ff00UL) * (right_ulong & 0x000000000000ff00UL)) & 0x000000000000ff00UL)
                                        | (((left_ulong & 0x0000000000ff0000UL) * (right_ulong & 0x0000000000ff0000UL)) & 0x0000000000ff0000UL)
                                        | (((left_ulong & 0x00000000ff000000UL) * (right_ulong & 0x00000000ff000000UL)) & 0x00000000ff000000UL)
                                        | (((left_ulong & 0x000000ff00000000UL) * (right_ulong & 0x000000ff00000000UL)) & 0x000000ff00000000UL)
                                        | (((left_ulong & 0x0000ff0000000000UL) * (right_ulong & 0x0000ff0000000000UL)) & 0x0000ff0000000000UL)
                                        | (((left_ulong & 0x00ff000000000000UL) * (right_ulong & 0x00ff000000000000UL)) & 0x00ff000000000000UL)
                                        | (((left_ulong & 0xff00000000000000UL) * (right_ulong & 0xff00000000000000UL)) & 0xff00000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt16))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x000000000000ffffUL) * (right_ulong & 0x000000000000ffffUL)) & 0x000000000000ffffUL)
                                        | (((left_ulong & 0x00000000ffff0000UL) * (right_ulong & 0x00000000ffff0000UL)) & 0x00000000ffff0000UL)
                                        | (((left_ulong & 0x0000ffff00000000UL) * (right_ulong & 0x0000ffff00000000UL)) & 0x0000ffff00000000UL)
                                        | (((left_ulong & 0xffff000000000000UL) * (right_ulong & 0xffff000000000000UL)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int16))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x000000000000ffffUL) * (right_ulong & 0x000000000000ffffUL)) & 0x000000000000ffffUL)
                                        | (((left_ulong & 0x00000000ffff0000UL) * (right_ulong & 0x00000000ffff0000UL)) & 0x00000000ffff0000UL)
                                        | (((left_ulong & 0x0000ffff00000000UL) * (right_ulong & 0x0000ffff00000000UL)) & 0x0000ffff00000000UL)
                                        | (((left_ulong & 0xffff000000000000UL) * (right_ulong & 0xffff000000000000UL)) & 0xffff000000000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt32))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000ffffffffUL) * (right_ulong & 0x00000000ffffffffUL)) & 0x00000000ffffffffUL)
                                        | (((left_ulong & 0xffffffff00000000UL) * (right_ulong & 0xffffffff00000000UL)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(Int32))
                    {
                        left_ulong = left._ulong;
                        right_ulong = right._ulong;
                        return new Vector<T>((((left_ulong & 0x00000000ffffffffUL) * (right_ulong & 0x00000000ffffffffUL)) & 0x00000000ffffffffUL)
                                        | (((left_ulong & 0xffffffff00000000UL) * (right_ulong & 0xffffffff00000000UL)) & 0xffffffff00000000UL), true);
                    }
                    else if (typeof(T) == typeof(UInt64))
                    {
                        return new Vector<T>(left._ulong * right._ulong, true);
                    }
                    else if (typeof(T) == typeof(Int64))
                    {
                        return new Vector<T>(left._ulong * right._ulong, true);
                    }
                    else if (typeof(T) == typeof(Single))
                    {
                        throwNotYetImplemented();
                        return new Vector<T>();
                    }
                    else if (typeof(T) == typeof(Double))
                    {
                        return new Vector<T>(left._double * right._double, true);
                    }
                    return new Vector<T>();
                }
            }
        }

        /// <summary>
        /// Negates a given vector.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The negated vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector<T> operator -(Vector<T> value)
        {
            return Zero - value;
        }
        #endregion Arithmetic Operators

        #region Bitwise Operators
        /// <summary>
        /// Returns a new vector by performing a bitwise-and operation on each of the elements in the given vectors.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The resultant vector.</returns>
        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> operator &(Vector<T> left, Vector<T> right)
        {
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    return left & right;
                }
                else
                {
                    if (typeof(T) == typeof(Double))
                    {
                        return new Vector<T>(BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(left._double) & BitConverter.DoubleToInt64Bits(right._ulong)), true);
                    }
                    else if (typeof(T) == typeof(Single))
                    {
                        // throw new System.Exception("NYI: need BitConverter ops for Single");
                        throwNotYetImplemented();
                        return new Vector<T>();
                    }
                    else
                    {
                        return new Vector<T>(left._ulong & right._ulong, true);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a new vector by performing a bitwise-or operation on each of the elements in the given vectors.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The resultant vector.</returns>
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> operator |(Vector<T> left, Vector<T> right)
        {
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    return left | right;
                }
                else
                {
                    if (typeof(T) == typeof(Double))
                    {
                        return new Vector<T>(BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(left._double) | BitConverter.DoubleToInt64Bits(right._ulong)), true);
                    }
                    else if (typeof(T) == typeof(Single))
                    {
                        // throw new System.Exception("NYI: need BitConverter ops for Single");
                        throwNotYetImplemented();
                        return new Vector<T>();
                    }
                    else
                    {
                        return new Vector<T>(left._ulong | right._ulong, true);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a new vector by performing a bitwise-exclusive-or operation on each of the elements in the given vectors.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The resultant vector.</returns>
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> operator ^(Vector<T> left, Vector<T> right)
        {
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    return left ^ right;
                }
                else
                {
                    if (typeof(T) == typeof(Double))
                    {
                        return new Vector<T>(BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(left._double) ^ BitConverter.DoubleToInt64Bits(right._ulong)), true);
                    }
                    else if (typeof(T) == typeof(Single))
                    {
                        // throw new System.Exception("NYI: need BitConverter ops for Single");
                        throwNotYetImplemented();
                        return new Vector<T>();
                    }
                    else
                    {
                        return new Vector<T>(left._ulong ^ right._ulong, true);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a new vector whose elements are obtained by taking the one's complement of the given vector's elements.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The one's complement vector.</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static Vector<T> operator ~(Vector<T> value)
        {
            return AllOnes ^ value;
        }
        #endregion Bitwise Operators

        #region Logical Operators
        /// <summary>
        /// Returns a boolean indicating whether each pair of elements in the given vectors are equal.
        /// </summary>
        /// <param name="left">The first vector to compare.</param>
        /// <param name="right">The first vector to compare.</param>
        /// <returns>True if all elements are equal; False otherwise.</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector<T> left, Vector<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean indicating whether any single pair of elements in the given vectors are equal.
        /// </summary>
        /// <param name="left">The first vector to compare.</param>
        /// <param name="right">The second vector to compare.</param>
        /// <returns>True if any element pairs are equal; False if no element pairs are equal.</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector<T> left, Vector<T> right)
        {
            return !(left == right);
        }
        #endregion Logical Operators

        #region Conversions
        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<Byte>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<Byte>((ulong)BitConverter.DoubleToInt64Bits(value._double), true);
            }
            else
            {
                return new Vector<Byte>(value._ulong, true);
            }
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [CLSCompliant(false)]
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<SByte>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<SByte>((ulong)BitConverter.DoubleToInt64Bits(value._double), true);
            }
            else
            {
                return new Vector<SByte>(value._ulong, true);
            }
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [CLSCompliant(false)]
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<UInt16>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<UInt16>((ulong)BitConverter.DoubleToInt64Bits(value._double), true);
            }
            else
            {
                return new Vector<UInt16>(value._ulong, true);
            }
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<Int16>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<Int16>((ulong)BitConverter.DoubleToInt64Bits(value._double), true);
            }
            else
            {
                return new Vector<Int16>(value._ulong, true);
            }
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [CLSCompliant(false)]
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<UInt32>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<UInt32>((ulong)BitConverter.DoubleToInt64Bits(value._double), true);
            }
            else
            {
                return new Vector<UInt32>(value._ulong, true);
            }
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<Int32>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<Int32>((ulong)BitConverter.DoubleToInt64Bits(value._double), true);
            }
            else
            {
                return new Vector<Int32>(value._ulong, true);
            }
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [CLSCompliant(false)]
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<UInt64>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<UInt64>((ulong)BitConverter.DoubleToInt64Bits(value._double), true);
            }
            else
            {
                return new Vector<UInt64>(value._ulong, true);
            }
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<Int64>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<Int64>((ulong)BitConverter.DoubleToInt64Bits(value._double), true);
            }
            else
            {
                return new Vector<Int64>(value._ulong, true);
            }
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<Single>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<Single>((ulong)BitConverter.DoubleToInt64Bits(value._double), true);
            }
            else
            {
                return new Vector<Single>(value._ulong, true);
            }
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector<Double>(Vector<T> value)
        {
            if (typeof(T) == typeof(Double))
            {
                return new Vector<Double>(value._double, true);
            }
            else
            {
                return new Vector<Double>(BitConverter.Int64BitsToDouble((long)value._ulong), true);
            }
        }

        #endregion Conversions

        #region Internal Comparison Methods
        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Vector<T> Equals(Vector<T> left, Vector<T> right)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return Equals(left, right);
            }
            else
            {
                ulong left_ulong;
                ulong right_ulong;
                if ((typeof(T) == typeof(Byte)) || (typeof(T) == typeof(SByte)))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((left_ulong & 0x00000000000000ffUL) == (left_ulong & 0x00000000000000ffUL)) ? 0x00000000000000ffUL : 0UL)
                                  | (((left_ulong & 0x000000000000ff00UL) == (left_ulong & 0x000000000000ff00UL)) ? 0x000000000000ff00UL : 0UL)
                                  | (((left_ulong & 0x0000000000ff0000UL) == (left_ulong & 0x0000000000ff0000UL)) ? 0x0000000000ff0000UL : 0UL)
                                  | (((left_ulong & 0x00000000ff000000UL) == (left_ulong & 0x00000000ff000000UL)) ? 0x00000000ff000000UL : 0UL)
                                  | (((left_ulong & 0x000000ff00000000UL) == (left_ulong & 0x000000ff00000000UL)) ? 0x000000ff00000000UL : 0UL)
                                  | (((left_ulong & 0x0000ff0000000000UL) == (left_ulong & 0x0000ff0000000000UL)) ? 0x0000ff0000000000UL : 0UL)
                                  | (((left_ulong & 0x00ff000000000000UL) == (left_ulong & 0x00ff000000000000UL)) ? 0x00ff000000000000UL : 0UL)
                                  | (((left_ulong & 0xff00000000000000UL) == (left_ulong & 0xff00000000000000UL)) ? 0xff00000000000000UL : 0UL), true);
                }
                else if ((typeof(T) == typeof(UInt16)) || (typeof(T) == typeof(Int16)))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((left_ulong & 0x000000000000ffffUL) == (left_ulong & 0x000000000000ffffUL)) ? 0x000000000000ffffUL : 0UL)
                                  | (((left_ulong & 0x00000000ffff0000UL) == (left_ulong & 0x00000000ffff0000UL)) ? 0x00000000ffff0000UL : 0UL)
                                  | (((left_ulong & 0x0000ffff00000000UL) == (left_ulong & 0x0000ffff00000000UL)) ? 0x0000ffff00000000UL : 0UL)
                                  | (((left_ulong & 0xffff000000000000UL) == (left_ulong & 0xffff000000000000UL)) ? 0xffff000000000000UL : 0UL), true);
                }
                else if ((typeof(T) == typeof(UInt32)) || (typeof(T) == typeof(Int32)))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((left_ulong & 0x00000000ffffffffUL) == (left_ulong & 0x00000000ffffffffUL)) ? 0x00000000ffffffffUL : 0UL)
                                  | (((left_ulong & 0xffffffff00000000UL) == (left_ulong & 0xffffffff00000000UL)) ? 0xffffffff00000000UL : 0UL), true);
                }
                else if ((typeof(T) == typeof(UInt64)) || (typeof(T) == typeof(Int64)))
                {
                    return new Vector<T>((left._ulong == right._ulong ? 0xffffffffffffffffUL : 0UL), true);
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                    return new Vector<T>();
                }
                else if (typeof(T) == typeof(Double))
                {
                    return new Vector<T>(BitConverter.Int64BitsToDouble(left._double == right._double ? ~0L : 0L), true);
                }
                else
                {
                    throwNotSupported();
                    return new Vector<T>();
                }
            }
        }

        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Vector<T> LessThan(Vector<T> left, Vector<T> right)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return LessThan(left, right);
            }
            else
            {
                ulong left_ulong;
                ulong right_ulong;
                if (typeof(T) == typeof(Byte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Byte)(left_ulong >> 00) < (Byte)(right_ulong >> 00) ? 0x00000000000000ffUL : 0UL)
                                  | ((Byte)(left_ulong >> 08) < (Byte)(right_ulong >> 08) ? 0x000000000000ff00UL : 0UL)
                                  | ((Byte)(left_ulong >> 16) < (Byte)(right_ulong >> 16) ? 0x0000000000ff0000UL : 0UL)
                                  | ((Byte)(left_ulong >> 24) < (Byte)(right_ulong >> 24) ? 0x00000000ff000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 32) < (Byte)(right_ulong >> 32) ? 0x000000ff00000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 40) < (Byte)(right_ulong >> 40) ? 0x0000ff0000000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 48) < (Byte)(right_ulong >> 48) ? 0x00ff000000000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 56) < (Byte)(right_ulong >> 56) ? 0xff00000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(SByte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((SByte)(left_ulong >> 00) < (SByte)(right_ulong >> 00) ? 0x00000000000000ffUL : 0UL)
                                  | ((SByte)(left_ulong >> 08) < (SByte)(right_ulong >> 08) ? 0x000000000000ff00UL : 0UL)
                                  | ((SByte)(left_ulong >> 16) < (SByte)(right_ulong >> 16) ? 0x0000000000ff0000UL : 0UL)
                                  | ((SByte)(left_ulong >> 24) < (SByte)(right_ulong >> 24) ? 0x00000000ff000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 32) < (SByte)(right_ulong >> 32) ? 0x000000ff00000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 40) < (SByte)(right_ulong >> 40) ? 0x0000ff0000000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 48) < (SByte)(right_ulong >> 48) ? 0x00ff000000000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 56) < (SByte)(right_ulong >> 56) ? 0xff00000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((UInt16)(left_ulong >> 00) < (UInt16)(right_ulong >> 00) ? 0x000000000000ffffUL : 0UL)
                                  | ((UInt16)(left_ulong >> 16) < (UInt16)(right_ulong >> 16) ? 0x00000000ffff0000UL : 0UL)
                                  | ((UInt16)(left_ulong >> 32) < (UInt16)(right_ulong >> 32) ? 0x0000ffff00000000UL : 0UL)
                                  | ((UInt16)(left_ulong >> 48) < (UInt16)(right_ulong >> 48) ? 0xffff000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int16)(left_ulong >> 00) < (Int16)(right_ulong >> 00) ? 0x000000000000ffffUL : 0UL)
                                  | ((Int16)(left_ulong >> 16) < (Int16)(right_ulong >> 16) ? 0x00000000ffff0000UL : 0UL)
                                  | ((Int16)(left_ulong >> 32) < (Int16)(right_ulong >> 32) ? 0x0000ffff00000000UL : 0UL)
                                  | ((Int16)(left_ulong >> 48) < (Int16)(right_ulong >> 48) ? 0xffff000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((UInt32)(left_ulong >> 00) < (UInt32)(right_ulong >> 00) ? 0x00000000ffffffffUL : 0UL)
                                  | ((UInt32)(left_ulong >> 32) < (UInt32)(right_ulong >> 32) ? 0xffffffff00000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int32)(left_ulong >> 00) < (Int32)(right_ulong >> 00) ? 0x00000000ffffffffUL : 0UL)
                                  | ((Int32)(left_ulong >> 32) < (Int32)(right_ulong >> 32) ? 0xffffffff00000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    return new Vector<T>((left._ulong < right._ulong ? ~0UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int64))
                {
                    return new Vector<T>(((long)left._ulong < (long)right._ulong ? ~0UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                    return new Vector<T>();
                }
                else if (typeof(T) == typeof(Double))
                {
                    return new Vector<T>((left._double < right._double ? ~0UL : 0UL), true);
                }
                else
                {
                    throwNotSupported();
                    return new Vector<T>();
                }
            }
        }

        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Vector<T> GreaterThan(Vector<T> left, Vector<T> right)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return GreaterThan(left, right);
            }
            else
            {
                ulong left_ulong;
                ulong right_ulong;
                if (typeof(T) == typeof(Byte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Byte)(left_ulong >> 00) > (Byte)(right_ulong >> 00) ? 0x00000000000000ffUL : 0UL)
                                  | ((Byte)(left_ulong >> 08) > (Byte)(right_ulong >> 08) ? 0x000000000000ff00UL : 0UL)
                                  | ((Byte)(left_ulong >> 16) > (Byte)(right_ulong >> 16) ? 0x0000000000ff0000UL : 0UL)
                                  | ((Byte)(left_ulong >> 24) > (Byte)(right_ulong >> 24) ? 0x00000000ff000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 32) > (Byte)(right_ulong >> 32) ? 0x000000ff00000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 40) > (Byte)(right_ulong >> 40) ? 0x0000ff0000000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 48) > (Byte)(right_ulong >> 48) ? 0x00ff000000000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 56) > (Byte)(right_ulong >> 56) ? 0xff00000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(SByte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((SByte)(left_ulong >> 00) > (SByte)(right_ulong >> 00) ? 0x00000000000000ffUL : 0UL)
                                  | ((SByte)(left_ulong >> 08) > (SByte)(right_ulong >> 08) ? 0x000000000000ff00UL : 0UL)
                                  | ((SByte)(left_ulong >> 16) > (SByte)(right_ulong >> 16) ? 0x0000000000ff0000UL : 0UL)
                                  | ((SByte)(left_ulong >> 24) > (SByte)(right_ulong >> 24) ? 0x00000000ff000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 32) > (SByte)(right_ulong >> 32) ? 0x000000ff00000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 40) > (SByte)(right_ulong >> 40) ? 0x0000ff0000000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 48) > (SByte)(right_ulong >> 48) ? 0x00ff000000000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 56) > (SByte)(right_ulong >> 56) ? 0xff00000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((UInt16)(left_ulong >> 00) > (UInt16)(right_ulong >> 00) ? 0x000000000000ffffUL : 0UL)
                                  | ((UInt16)(left_ulong >> 16) > (UInt16)(right_ulong >> 16) ? 0x00000000ffff0000UL : 0UL)
                                  | ((UInt16)(left_ulong >> 32) > (UInt16)(right_ulong >> 32) ? 0x0000ffff00000000UL : 0UL)
                                  | ((UInt16)(left_ulong >> 48) > (UInt16)(right_ulong >> 48) ? 0xffff000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int16)(left_ulong >> 00) > (Int16)(right_ulong >> 00) ? 0x000000000000ffffUL : 0UL)
                                  | ((Int16)(left_ulong >> 16) > (Int16)(right_ulong >> 16) ? 0x00000000ffff0000UL : 0UL)
                                  | ((Int16)(left_ulong >> 32) > (Int16)(right_ulong >> 32) ? 0x0000ffff00000000UL : 0UL)
                                  | ((Int16)(left_ulong >> 48) > (Int16)(right_ulong >> 48) ? 0xffff000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((UInt32)(left_ulong >> 00) > (UInt32)(right_ulong >> 00) ? 0x00000000ffffffffUL : 0UL)
                                  | ((UInt32)(left_ulong >> 32) > (UInt32)(right_ulong >> 32) ? 0xffffffff00000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int32)(left_ulong >> 00) > (Int32)(right_ulong >> 00) ? 0x00000000ffffffffUL : 0UL)
                                  | ((Int32)(left_ulong >> 32) > (Int32)(right_ulong >> 32) ? 0xffffffff00000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    return new Vector<T>((left._ulong > right._ulong ? ~0UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int64))
                {
                    return new Vector<T>(((long)left._ulong > (long)right._ulong ? ~0UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                    return new Vector<T>();
                }
                else if (typeof(T) == typeof(Double))
                {
                    return new Vector<T>((left._double > right._double ? ~0UL : 0UL), true);
                }
                else
                {
                    throwNotSupported();
                    return new Vector<T>();
                }
            }
        }

        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        internal static Vector<T> GreaterThanOrEqual(Vector<T> left, Vector<T> right)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return GreaterThanOrEqual(left, right);
            }
            else
            {
                ulong left_ulong;
                ulong right_ulong;
                if (typeof(T) == typeof(Byte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Byte)(left_ulong >> 00) >= (Byte)(right_ulong >> 00) ? 0x00000000000000ffUL : 0UL)
                                  | ((Byte)(left_ulong >> 08) >= (Byte)(right_ulong >> 08) ? 0x000000000000ff00UL : 0UL)
                                  | ((Byte)(left_ulong >> 16) >= (Byte)(right_ulong >> 16) ? 0x0000000000ff0000UL : 0UL)
                                  | ((Byte)(left_ulong >> 24) >= (Byte)(right_ulong >> 24) ? 0x00000000ff000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 32) >= (Byte)(right_ulong >> 32) ? 0x000000ff00000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 40) >= (Byte)(right_ulong >> 40) ? 0x0000ff0000000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 48) >= (Byte)(right_ulong >> 48) ? 0x00ff000000000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 56) >= (Byte)(right_ulong >> 56) ? 0xff00000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(SByte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((SByte)(left_ulong >> 00) >= (SByte)(right_ulong >> 00) ? 0x00000000000000ffUL : 0UL)
                                  | ((SByte)(left_ulong >> 08) >= (SByte)(right_ulong >> 08) ? 0x000000000000ff00UL : 0UL)
                                  | ((SByte)(left_ulong >> 16) >= (SByte)(right_ulong >> 16) ? 0x0000000000ff0000UL : 0UL)
                                  | ((SByte)(left_ulong >> 24) >= (SByte)(right_ulong >> 24) ? 0x00000000ff000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 32) >= (SByte)(right_ulong >> 32) ? 0x000000ff00000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 40) >= (SByte)(right_ulong >> 40) ? 0x0000ff0000000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 48) >= (SByte)(right_ulong >> 48) ? 0x00ff000000000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 56) >= (SByte)(right_ulong >> 56) ? 0xff00000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((UInt16)(left_ulong >> 00) >= (UInt16)(right_ulong >> 00) ? 0x000000000000ffffUL : 0UL)
                                  | ((UInt16)(left_ulong >> 16) >= (UInt16)(right_ulong >> 16) ? 0x00000000ffff0000UL : 0UL)
                                  | ((UInt16)(left_ulong >> 32) >= (UInt16)(right_ulong >> 32) ? 0x0000ffff00000000UL : 0UL)
                                  | ((UInt16)(left_ulong >> 48) >= (UInt16)(right_ulong >> 48) ? 0xffff000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int16)(left_ulong >> 00) >= (Int16)(right_ulong >> 00) ? 0x000000000000ffffUL : 0UL)
                                  | ((Int16)(left_ulong >> 16) >= (Int16)(right_ulong >> 16) ? 0x00000000ffff0000UL : 0UL)
                                  | ((Int16)(left_ulong >> 32) >= (Int16)(right_ulong >> 32) ? 0x0000ffff00000000UL : 0UL)
                                  | ((Int16)(left_ulong >> 48) >= (Int16)(right_ulong >> 48) ? 0xffff000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((UInt32)(left_ulong >> 00) >= (UInt32)(right_ulong >> 00) ? 0x00000000ffffffffUL : 0UL)
                                  | ((UInt32)(left_ulong >> 32) >= (UInt32)(right_ulong >> 32) ? 0xffffffff00000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int32)(left_ulong >> 00) >= (Int32)(right_ulong >> 00) ? 0x00000000ffffffffUL : 0UL)
                                  | ((Int32)(left_ulong >> 32) >= (Int32)(right_ulong >> 32) ? 0xffffffff00000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    return new Vector<T>((left._ulong >= right._ulong ? ~0UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int64))
                {
                    return new Vector<T>(((long)left._ulong >= (long)right._ulong ? ~0UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                    return new Vector<T>();
                }
                else if (typeof(T) == typeof(Double))
                {
                    return new Vector<T>((left._double >= right._double ? ~0UL : 0UL), true);
                }
                else
                {
                    throwNotSupported();
                    return new Vector<T>();
                }
            }
        }

        [JitIntrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        internal static Vector<T> LessThanOrEqual(Vector<T> left, Vector<T> right)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return LessThanOrEqual(left, right);
            }
            else
            {
                ulong left_ulong;
                ulong right_ulong;
                if (typeof(T) == typeof(Byte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Byte)(left_ulong >> 00) <= (Byte)(right_ulong >> 00) ? 0x00000000000000ffUL : 0UL)
                                  | ((Byte)(left_ulong >> 08) <= (Byte)(right_ulong >> 08) ? 0x000000000000ff00UL : 0UL)
                                  | ((Byte)(left_ulong >> 16) <= (Byte)(right_ulong >> 16) ? 0x0000000000ff0000UL : 0UL)
                                  | ((Byte)(left_ulong >> 24) <= (Byte)(right_ulong >> 24) ? 0x00000000ff000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 32) <= (Byte)(right_ulong >> 32) ? 0x000000ff00000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 40) <= (Byte)(right_ulong >> 40) ? 0x0000ff0000000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 48) <= (Byte)(right_ulong >> 48) ? 0x00ff000000000000UL : 0UL)
                                  | ((Byte)(left_ulong >> 56) <= (Byte)(right_ulong >> 56) ? 0xff00000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(SByte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((SByte)(left_ulong >> 00) <= (SByte)(right_ulong >> 00) ? 0x00000000000000ffUL : 0UL)
                                  | ((SByte)(left_ulong >> 08) <= (SByte)(right_ulong >> 08) ? 0x000000000000ff00UL : 0UL)
                                  | ((SByte)(left_ulong >> 16) <= (SByte)(right_ulong >> 16) ? 0x0000000000ff0000UL : 0UL)
                                  | ((SByte)(left_ulong >> 24) <= (SByte)(right_ulong >> 24) ? 0x00000000ff000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 32) <= (SByte)(right_ulong >> 32) ? 0x000000ff00000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 40) <= (SByte)(right_ulong >> 40) ? 0x0000ff0000000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 48) <= (SByte)(right_ulong >> 48) ? 0x00ff000000000000UL : 0UL)
                                  | ((SByte)(left_ulong >> 56) <= (SByte)(right_ulong >> 56) ? 0xff00000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((UInt16)(left_ulong >> 00) <= (UInt16)(right_ulong >> 00) ? 0x000000000000ffffUL : 0UL)
                                  | ((UInt16)(left_ulong >> 16) <= (UInt16)(right_ulong >> 16) ? 0x00000000ffff0000UL : 0UL)
                                  | ((UInt16)(left_ulong >> 32) <= (UInt16)(right_ulong >> 32) ? 0x0000ffff00000000UL : 0UL)
                                  | ((UInt16)(left_ulong >> 48) <= (UInt16)(right_ulong >> 48) ? 0xffff000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int16)(left_ulong >> 00) <= (Int16)(right_ulong >> 00) ? 0x000000000000ffffUL : 0UL)
                                  | ((Int16)(left_ulong >> 16) <= (Int16)(right_ulong >> 16) ? 0x00000000ffff0000UL : 0UL)
                                  | ((Int16)(left_ulong >> 32) <= (Int16)(right_ulong >> 32) ? 0x0000ffff00000000UL : 0UL)
                                  | ((Int16)(left_ulong >> 48) <= (Int16)(right_ulong >> 48) ? 0xffff000000000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((UInt32)(left_ulong >> 00) <= (UInt32)(right_ulong >> 00) ? 0x00000000ffffffffUL : 0UL)
                                  | ((UInt32)(left_ulong >> 32) <= (UInt32)(right_ulong >> 32) ? 0xffffffff00000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int32)(left_ulong >> 00) <= (Int32)(right_ulong >> 00) ? 0x00000000ffffffffUL : 0UL)
                                  | ((Int32)(left_ulong >> 32) <= (Int32)(right_ulong >> 32) ? 0xffffffff00000000UL : 0UL), true);
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    return new Vector<T>((left._ulong <= right._ulong ? ~0UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Int64))
                {
                    return new Vector<T>(((long)left._ulong <= (long)right._ulong ? ~0UL : 0UL), true);
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                    return new Vector<T>();
                }
                else if (typeof(T) == typeof(Double))
                {
                    return new Vector<T>((left._double <= right._double ? ~0UL : 0UL), true);
                }
                else
                {
                    throwNotSupported();
                    return new Vector<T>();
                }
            }
        }

        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector<T> ConditionalSelect(Vector<T> condition, Vector<T> left, Vector<T> right)
        {
            return (left & condition) | (Vector.AndNot(right, condition));
        }
        #endregion Comparison Methods

        #region Internal Math Methods
        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Vector<T> Abs(Vector<T> value)
        {
            if (typeof(T) == typeof(Byte))
            {
                return value;
            }
            else if (typeof(T) == typeof(UInt16))
            {
                return value;
            }
            else if (typeof(T) == typeof(UInt32))
            {
                return value;
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return value;
            }
            if (Vector.IsHardwareAccelerated)
            {
                return Abs(value);
            }
            else
            {
                if (typeof(T) == typeof(SByte))
                {
                    var value_ulong = value._ulong;
                    return new Vector<T>((((ulong)(Byte)Math.Abs((SByte)(value_ulong >> 00))) << 00)
                                  | (((ulong)(Byte)Math.Abs((SByte)(value_ulong >> 08))) << 08)
                                  | (((ulong)(Byte)Math.Abs((SByte)(value_ulong >> 16))) << 16)
                                  | (((ulong)(Byte)Math.Abs((SByte)(value_ulong >> 24))) << 24)
                                  | (((ulong)(Byte)Math.Abs((SByte)(value_ulong >> 32))) << 32)
                                  | (((ulong)(Byte)Math.Abs((SByte)(value_ulong >> 40))) << 40)
                                  | (((ulong)(Byte)Math.Abs((SByte)(value_ulong >> 48))) << 48)
                                  | (((ulong)(Byte)Math.Abs((SByte)(value_ulong >> 56))) << 56), true);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    var value_ulong = value._ulong;
                    return new Vector<T>((((ulong)(UInt16)Math.Abs((Int16)(value_ulong >> 00))) << 00)
                                  | (((ulong)(UInt16)Math.Abs((Int16)(value_ulong >> 16))) << 16)
                                  | (((ulong)(UInt16)Math.Abs((Int16)(value_ulong >> 32))) << 32)
                                  | (((ulong)(UInt16)Math.Abs((Int16)(value_ulong >> 48))) << 48), true);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    var value_ulong = value._ulong;
                    return new Vector<T>((((ulong)(UInt32)Math.Abs((Int32)(value_ulong >> 00))) << 00)
                                  | (((ulong)(UInt32)Math.Abs((Int32)(value_ulong >> 32))) << 32), true);
                }
                else if (typeof(T) == typeof(Int64))
                {
                    return new Vector<T>((ulong)Math.Abs((Int64)value._ulong), true);
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                    return new Vector<T>();
                }
                else if (typeof(T) == typeof(Double))
                {
                    return new Vector<T>(Math.Abs(value._double), true);
                }
                else
                {
                    throwNotSupported();
                    return new Vector<T>();
                }
            }
        }

        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Vector<T> Min(Vector<T> left, Vector<T> right)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return Min(left, right);
            }
            else
            {
                ulong left_ulong;
                ulong right_ulong;
                if (typeof(T) == typeof(Byte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((Byte)(left_ulong >> 00) < (Byte)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x00000000000000ffUL)
                                  | (((Byte)(left_ulong >> 08) < (Byte)(right_ulong >> 08) ? left_ulong : right_ulong) & 0x000000000000ff00UL)
                                  | (((Byte)(left_ulong >> 16) < (Byte)(right_ulong >> 16) ? left_ulong : right_ulong) & 0x0000000000ff0000UL)
                                  | (((Byte)(left_ulong >> 24) < (Byte)(right_ulong >> 24) ? left_ulong : right_ulong) & 0x00000000ff000000UL)
                                  | (((Byte)(left_ulong >> 32) < (Byte)(right_ulong >> 32) ? left_ulong : right_ulong) & 0x000000ff00000000UL)
                                  | (((Byte)(left_ulong >> 40) < (Byte)(right_ulong >> 40) ? left_ulong : right_ulong) & 0x0000ff0000000000UL)
                                  | (((Byte)(left_ulong >> 48) < (Byte)(right_ulong >> 48) ? left_ulong : right_ulong) & 0x00ff000000000000UL)
                                  | (((Byte)(left_ulong >> 56) < (Byte)(right_ulong >> 56) ? left_ulong : right_ulong) & 0xff00000000000000UL), true);
                }
                else if (typeof(T) == typeof(SByte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((SByte)(left_ulong >> 00) < (SByte)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x00000000000000ffUL)
                                  | (((SByte)(left_ulong >> 08) < (SByte)(right_ulong >> 08) ? left_ulong : right_ulong) & 0x000000000000ff00UL)
                                  | (((SByte)(left_ulong >> 16) < (SByte)(right_ulong >> 16) ? left_ulong : right_ulong) & 0x0000000000ff0000UL)
                                  | (((SByte)(left_ulong >> 24) < (SByte)(right_ulong >> 24) ? left_ulong : right_ulong) & 0x00000000ff000000UL)
                                  | (((SByte)(left_ulong >> 32) < (SByte)(right_ulong >> 32) ? left_ulong : right_ulong) & 0x000000ff00000000UL)
                                  | (((SByte)(left_ulong >> 40) < (SByte)(right_ulong >> 40) ? left_ulong : right_ulong) & 0x0000ff0000000000UL)
                                  | (((SByte)(left_ulong >> 48) < (SByte)(right_ulong >> 48) ? left_ulong : right_ulong) & 0x00ff000000000000UL)
                                  | (((SByte)(left_ulong >> 56) < (SByte)(right_ulong >> 56) ? left_ulong : right_ulong) & 0xff00000000000000UL), true);
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((UInt16)(left_ulong >> 00) < (UInt16)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x000000000000ffffUL)
                                  | (((UInt16)(left_ulong >> 16) < (UInt16)(right_ulong >> 16) ? left_ulong : right_ulong) & 0x00000000ffff0000UL)
                                  | (((UInt16)(left_ulong >> 32) < (UInt16)(right_ulong >> 32) ? left_ulong : right_ulong) & 0x0000ffff00000000UL)
                                  | (((UInt16)(left_ulong >> 48) < (UInt16)(right_ulong >> 48) ? left_ulong : right_ulong) & 0xffff000000000000UL), true);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((Int16)(left_ulong >> 00) < (Int16)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x000000000000ffffUL)
                                  | (((Int16)(left_ulong >> 16) < (Int16)(right_ulong >> 16) ? left_ulong : right_ulong) & 0x00000000ffff0000UL)
                                  | (((Int16)(left_ulong >> 32) < (Int16)(right_ulong >> 32) ? left_ulong : right_ulong) & 0x0000ffff00000000UL)
                                  | (((Int16)(left_ulong >> 48) < (Int16)(right_ulong >> 48) ? left_ulong : right_ulong) & 0xffff000000000000UL), true);
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((UInt32)(left_ulong >> 00) < (UInt32)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x00000000ffffffffUL)
                                  | (((UInt32)(left_ulong >> 32) < (UInt32)(right_ulong >> 32) ? left_ulong : right_ulong) & 0xffffffff00000000UL), true);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((Int32)(left_ulong >> 00) < (Int32)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x00000000ffffffffUL)
                                  | (((Int32)(left_ulong >> 32) < (Int32)(right_ulong >> 32) ? left_ulong : right_ulong) & 0xffffffff00000000UL), true);
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((left_ulong < right_ulong ? left_ulong : right_ulong), true);
                }
                else if (typeof(T) == typeof(Int64))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int64)left_ulong < (Int64)right_ulong ? left_ulong : right_ulong), true);
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                    return new Vector<T>();
                }
                else if (typeof(T) == typeof(Double))
                {
                    var left_double = left._double;
                    var right_double = right._double;
                    return new Vector<T>((left_double < right_double ? left_double : right_double), true);
                }
                else
                {
                    throwNotSupported();
                    return new Vector<T>();
                }
            }
        }

        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Vector<T> Max(Vector<T> left, Vector<T> right)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return Max(left, right);
            }
            else
            {
                ulong left_ulong;
                ulong right_ulong;
                if (typeof(T) == typeof(Byte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((Byte)(left_ulong >> 00) > (Byte)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x00000000000000ffUL)
                                  | (((Byte)(left_ulong >> 08) > (Byte)(right_ulong >> 08) ? left_ulong : right_ulong) & 0x000000000000ff00UL)
                                  | (((Byte)(left_ulong >> 16) > (Byte)(right_ulong >> 16) ? left_ulong : right_ulong) & 0x0000000000ff0000UL)
                                  | (((Byte)(left_ulong >> 24) > (Byte)(right_ulong >> 24) ? left_ulong : right_ulong) & 0x00000000ff000000UL)
                                  | (((Byte)(left_ulong >> 32) > (Byte)(right_ulong >> 32) ? left_ulong : right_ulong) & 0x000000ff00000000UL)
                                  | (((Byte)(left_ulong >> 40) > (Byte)(right_ulong >> 40) ? left_ulong : right_ulong) & 0x0000ff0000000000UL)
                                  | (((Byte)(left_ulong >> 48) > (Byte)(right_ulong >> 48) ? left_ulong : right_ulong) & 0x00ff000000000000UL)
                                  | (((Byte)(left_ulong >> 56) > (Byte)(right_ulong >> 56) ? left_ulong : right_ulong) & 0xff00000000000000UL), true);
                }
                else if (typeof(T) == typeof(SByte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((SByte)(left_ulong >> 00) > (SByte)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x00000000000000ffUL)
                                  | (((SByte)(left_ulong >> 08) > (SByte)(right_ulong >> 08) ? left_ulong : right_ulong) & 0x000000000000ff00UL)
                                  | (((SByte)(left_ulong >> 16) > (SByte)(right_ulong >> 16) ? left_ulong : right_ulong) & 0x0000000000ff0000UL)
                                  | (((SByte)(left_ulong >> 24) > (SByte)(right_ulong >> 24) ? left_ulong : right_ulong) & 0x00000000ff000000UL)
                                  | (((SByte)(left_ulong >> 32) > (SByte)(right_ulong >> 32) ? left_ulong : right_ulong) & 0x000000ff00000000UL)
                                  | (((SByte)(left_ulong >> 40) > (SByte)(right_ulong >> 40) ? left_ulong : right_ulong) & 0x0000ff0000000000UL)
                                  | (((SByte)(left_ulong >> 48) > (SByte)(right_ulong >> 48) ? left_ulong : right_ulong) & 0x00ff000000000000UL)
                                  | (((SByte)(left_ulong >> 56) > (SByte)(right_ulong >> 56) ? left_ulong : right_ulong) & 0xff00000000000000UL), true);
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((UInt16)(left_ulong >> 00) > (UInt16)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x000000000000ffffUL)
                                  | (((UInt16)(left_ulong >> 16) > (UInt16)(right_ulong >> 16) ? left_ulong : right_ulong) & 0x00000000ffff0000UL)
                                  | (((UInt16)(left_ulong >> 32) > (UInt16)(right_ulong >> 32) ? left_ulong : right_ulong) & 0x0000ffff00000000UL)
                                  | (((UInt16)(left_ulong >> 48) > (UInt16)(right_ulong >> 48) ? left_ulong : right_ulong) & 0xffff000000000000UL), true);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((Int16)(left_ulong >> 00) > (Int16)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x000000000000ffffUL)
                                  | (((Int16)(left_ulong >> 16) > (Int16)(right_ulong >> 16) ? left_ulong : right_ulong) & 0x00000000ffff0000UL)
                                  | (((Int16)(left_ulong >> 32) > (Int16)(right_ulong >> 32) ? left_ulong : right_ulong) & 0x0000ffff00000000UL)
                                  | (((Int16)(left_ulong >> 48) > (Int16)(right_ulong >> 48) ? left_ulong : right_ulong) & 0xffff000000000000UL), true);
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((UInt32)(left_ulong >> 00) > (UInt32)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x00000000ffffffffUL)
                                  | (((UInt32)(left_ulong >> 32) > (UInt32)(right_ulong >> 32) ? left_ulong : right_ulong) & 0xffffffff00000000UL), true);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((((Int32)(left_ulong >> 00) > (Int32)(right_ulong >> 00) ? left_ulong : right_ulong) & 0x00000000ffffffffUL)
                                  | (((Int32)(left_ulong >> 32) > (Int32)(right_ulong >> 32) ? left_ulong : right_ulong) & 0xffffffff00000000UL), true);
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>((left_ulong > right_ulong ? left_ulong : right_ulong), true);
                }
                else if (typeof(T) == typeof(Int64))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    return new Vector<T>(((Int64)left_ulong > (Int64)right_ulong ? left_ulong : right_ulong), true);
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                    return new Vector<T>();
                }
                else if (typeof(T) == typeof(Double))
                {
                    var left_double = left._double;
                    var right_double = right._double;
                    return new Vector<T>((left_double > right_double ? left_double : right_double), true);
                }
                else
                {
                    throwNotSupported();
                    return new Vector<T>();
                }
            }
        }

        [JitIntrinsic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T DotProduct(Vector<T> left, Vector<T> right)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return DotProduct(left, right);
            }
            else
            {
                ulong left_ulong;
                ulong right_ulong;
                if (typeof(T) == typeof(Byte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    int product = (Byte)(left_ulong >> 00) * (Byte)(right_ulong >> 00)
                                + (Byte)(left_ulong >> 08) * (Byte)(right_ulong >> 08)
                                + (Byte)(left_ulong >> 16) * (Byte)(right_ulong >> 16)
                                + (Byte)(left_ulong >> 24) * (Byte)(right_ulong >> 24)
                                + (Byte)(left_ulong >> 32) * (Byte)(right_ulong >> 32)
                                + (Byte)(left_ulong >> 40) * (Byte)(right_ulong >> 40)
                                + (Byte)(left_ulong >> 48) * (Byte)(right_ulong >> 48)
                                + (Byte)(left_ulong >> 56) * (Byte)(right_ulong >> 56);
                    return (T)(object)(Byte)product;
                }
                else if (typeof(T) == typeof(SByte))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    int product = (SByte)(left_ulong >> 00) * (SByte)(right_ulong >> 00)
                                + (SByte)(left_ulong >> 08) * (SByte)(right_ulong >> 08)
                                + (SByte)(left_ulong >> 16) * (SByte)(right_ulong >> 16)
                                + (SByte)(left_ulong >> 24) * (SByte)(right_ulong >> 24)
                                + (SByte)(left_ulong >> 32) * (SByte)(right_ulong >> 32)
                                + (SByte)(left_ulong >> 40) * (SByte)(right_ulong >> 40)
                                + (SByte)(left_ulong >> 48) * (SByte)(right_ulong >> 48)
                                + (SByte)(left_ulong >> 56) * (SByte)(right_ulong >> 56);
                    return (T)(object)(SByte)product;
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    int product = (UInt16)(left_ulong >> 00) * (UInt16)(right_ulong >> 00)
                                + (UInt16)(left_ulong >> 16) * (UInt16)(right_ulong >> 16)
                                + (UInt16)(left_ulong >> 32) * (UInt16)(right_ulong >> 32)
                                + (UInt16)(left_ulong >> 48) * (UInt16)(right_ulong >> 48);
                    return (T)(object)(UInt16)product;
                }
                else if (typeof(T) == typeof(Int16))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    int product = (Int16)(left_ulong >> 00) * (Int16)(right_ulong >> 00)
                                + (Int16)(left_ulong >> 16) * (Int16)(right_ulong >> 16)
                                + (Int16)(left_ulong >> 32) * (Int16)(right_ulong >> 32)
                                + (Int16)(left_ulong >> 48) * (Int16)(right_ulong >> 48);
                    return (T)(object)(Int16)product;
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    UInt32 product = (UInt32)(left_ulong >> 00) * (UInt32)(right_ulong >> 00)
                                   + (UInt32)(left_ulong >> 32) * (UInt32)(right_ulong >> 32);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(Int32))
                {
                    left_ulong = left._ulong;
                    right_ulong = right._ulong;
                    Int32 product = (Int32)(left_ulong >> 00) * (Int32)(right_ulong >> 00)
                                  + (Int32)(left_ulong >> 32) * (Int32)(right_ulong >> 32);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    return (T)(object)(left._ulong * right._ulong);
                }
                else if (typeof(T) == typeof(Int64))
                {
                    Int64 product = ((Int64)left._ulong * (Int64)right._ulong);
                    return (T)(object)product;
                }
                else
                {
                    if (typeof(T) == typeof(Single))
                    {
                        throwNotYetImplemented();
                    }
                    if (typeof(T) != typeof(Double))
                    {
                        throwNotSupported();
                    }
                    return (T)(object)(left._double * right._double);
                }
            }
        }

        [JitIntrinsic]
        internal static unsafe Vector<T> SquareRoot(Vector<T> value)
        {
            if (Vector.IsHardwareAccelerated)
            {
                return SquareRoot(value);
            }
            else
            {
                ulong value_ulong;
                if (typeof(T) == typeof(Byte))
                {
                    value_ulong = value._ulong;
                    return new Vector<T>((((ulong)(Byte)Math.Sqrt((Byte)(value_ulong >> 00))) << 00)
                                  | (((ulong)(Byte)Math.Sqrt((Byte)(value_ulong >> 08))) << 08)
                                  | (((ulong)(Byte)Math.Sqrt((Byte)(value_ulong >> 16))) << 16)
                                  | (((ulong)(Byte)Math.Sqrt((Byte)(value_ulong >> 24))) << 24)
                                  | (((ulong)(Byte)Math.Sqrt((Byte)(value_ulong >> 32))) << 32)
                                  | (((ulong)(Byte)Math.Sqrt((Byte)(value_ulong >> 40))) << 40)
                                  | (((ulong)(Byte)Math.Sqrt((Byte)(value_ulong >> 48))) << 48)
                                  | (((ulong)(Byte)Math.Sqrt((Byte)(value_ulong >> 56))) << 56), true);
                }
                else if (typeof(T) == typeof(SByte))
                {
                    value_ulong = value._ulong;
                    return new Vector<T>((((ulong)(Byte)(SByte)Math.Sqrt((SByte)(value_ulong >> 00))) << 00)
                                  | (((ulong)(Byte)(SByte)Math.Sqrt((SByte)(value_ulong >> 08))) << 08)
                                  | (((ulong)(Byte)(SByte)Math.Sqrt((SByte)(value_ulong >> 16))) << 16)
                                  | (((ulong)(Byte)(SByte)Math.Sqrt((SByte)(value_ulong >> 24))) << 24)
                                  | (((ulong)(Byte)(SByte)Math.Sqrt((SByte)(value_ulong >> 32))) << 32)
                                  | (((ulong)(Byte)(SByte)Math.Sqrt((SByte)(value_ulong >> 40))) << 40)
                                  | (((ulong)(Byte)(SByte)Math.Sqrt((SByte)(value_ulong >> 48))) << 48)
                                  | (((ulong)(Byte)(SByte)Math.Sqrt((SByte)(value_ulong >> 56))) << 56), true);
                }
                else if (typeof(T) == typeof(UInt16))
                {
                    value_ulong = value._ulong;
                    return new Vector<T>((((ulong)(UInt16)Math.Sqrt((UInt16)(value_ulong >> 00))) << 00)
                                  | (((ulong)(UInt16)Math.Sqrt((UInt16)(value_ulong >> 16))) << 16)
                                  | (((ulong)(UInt16)Math.Sqrt((UInt16)(value_ulong >> 32))) << 32)
                                  | (((ulong)(UInt16)Math.Sqrt((UInt16)(value_ulong >> 48))) << 48), true);
                }
                else if (typeof(T) == typeof(Int16))
                {
                    value_ulong = value._ulong;
                    return new Vector<T>((((ulong)(UInt16)(Int16)Math.Sqrt((Int16)(value_ulong >> 00))) << 00)
                                  | (((ulong)(UInt16)(Int16)Math.Sqrt((Int16)(value_ulong >> 16))) << 16)
                                  | (((ulong)(UInt16)(Int16)Math.Sqrt((Int16)(value_ulong >> 32))) << 32)
                                  | (((ulong)(UInt16)(Int16)Math.Sqrt((Int16)(value_ulong >> 48))) << 48), true);
                }
                else if (typeof(T) == typeof(UInt32))
                {
                    value_ulong = value._ulong;
                    return new Vector<T>((((ulong)(UInt32)Math.Sqrt((UInt32)(value_ulong >> 00))) << 00)
                                  | (((ulong)(UInt32)Math.Sqrt((UInt32)(value_ulong >> 32))) << 32), true);
                }
                else if (typeof(T) == typeof(Int32))
                {
                    value_ulong = value._ulong;
                    return new Vector<T>((((ulong)(UInt32)(Int32)Math.Sqrt((Int32)(value_ulong >> 00))) << 00)
                                  | (((ulong)(UInt32)(Int32)Math.Sqrt((Int32)(value_ulong >> 32))) << 32), true);
                }
                else if (typeof(T) == typeof(UInt64))
                {
                    return new Vector<T>((UInt64)Math.Sqrt(value._ulong), true);
                }
                else if (typeof(T) == typeof(Int64))
                {
                    return new Vector<T>((ulong)(Int64)Math.Sqrt((Int64)value._ulong), true);
                }
                else if (typeof(T) == typeof(Single))
                {
                    throwNotYetImplemented();
                    return new Vector<T>();
                }
                else if (typeof(T) == typeof(Double))
                {
                    return new Vector<T>(Math.Sqrt(value._double), true);
                }
                else
                {
                    throwNotSupported();
                    return new Vector<T>();
                }
            }
        }
        #endregion Internal Math Methods

        #region Helper Methods
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static bool ScalarEquals(T left, T right)
        {
            if (typeof(T) == typeof(Byte))
            {
                return (Byte)(object)left == (Byte)(object)right;
            }
            else if (typeof(T) == typeof(SByte))
            {
                return (SByte)(object)left == (SByte)(object)right;
            }
            else if (typeof(T) == typeof(UInt16))
            {
                return (UInt16)(object)left == (UInt16)(object)right;
            }
            else if (typeof(T) == typeof(Int16))
            {
                return (Int16)(object)left == (Int16)(object)right;
            }
            else if (typeof(T) == typeof(UInt32))
            {
                return (UInt32)(object)left == (UInt32)(object)right;
            }
            else if (typeof(T) == typeof(Int32))
            {
                return (Int32)(object)left == (Int32)(object)right;
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return (UInt64)(object)left == (UInt64)(object)right;
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (Int64)(object)left == (Int64)(object)right;
            }
            else if (typeof(T) == typeof(Single))
            {
                return (Single)(object)left == (Single)(object)right;
            }
            else if (typeof(T) == typeof(Double))
            {
                return (Double)(object)left == (Double)(object)right;
            }
            else
            {
                return false; // throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static bool ScalarLessThan(T left, T right)
        {
            if (typeof(T) == typeof(Byte))
            {
                return (Byte)(object)left < (Byte)(object)right;
            }
            else if (typeof(T) == typeof(SByte))
            {
                return (SByte)(object)left < (SByte)(object)right;
            }
            else if (typeof(T) == typeof(UInt16))
            {
                return (UInt16)(object)left < (UInt16)(object)right;
            }
            else if (typeof(T) == typeof(Int16))
            {
                return (Int16)(object)left < (Int16)(object)right;
            }
            else if (typeof(T) == typeof(UInt32))
            {
                return (UInt32)(object)left < (UInt32)(object)right;
            }
            else if (typeof(T) == typeof(Int32))
            {
                return (Int32)(object)left < (Int32)(object)right;
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return (UInt64)(object)left < (UInt64)(object)right;
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (Int64)(object)left < (Int64)(object)right;
            }
            else if (typeof(T) == typeof(Single))
            {
                return (Single)(object)left < (Single)(object)right;
            }
            else if (typeof(T) == typeof(Double))
            {
                return (Double)(object)left < (Double)(object)right;
            }
            else
            {
                return false; // throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static bool ScalarGreaterThan(T left, T right)
        {
            if (typeof(T) == typeof(Byte))
            {
                return (Byte)(object)left > (Byte)(object)right;
            }
            else if (typeof(T) == typeof(SByte))
            {
                return (SByte)(object)left > (SByte)(object)right;
            }
            else if (typeof(T) == typeof(UInt16))
            {
                return (UInt16)(object)left > (UInt16)(object)right;
            }
            else if (typeof(T) == typeof(Int16))
            {
                return (Int16)(object)left > (Int16)(object)right;
            }
            else if (typeof(T) == typeof(UInt32))
            {
                return (UInt32)(object)left > (UInt32)(object)right;
            }
            else if (typeof(T) == typeof(Int32))
            {
                return (Int32)(object)left > (Int32)(object)right;
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return (UInt64)(object)left > (UInt64)(object)right;
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (Int64)(object)left > (Int64)(object)right;
            }
            else if (typeof(T) == typeof(Single))
            {
                return (Single)(object)left > (Single)(object)right;
            }
            else if (typeof(T) == typeof(Double))
            {
                return (Double)(object)left > (Double)(object)right;
            }
            else
            {
                throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static T ScalarAdd(T left, T right)
        {
            if (typeof(T) == typeof(Byte))
            {
                return (T)(object)(Byte)((Byte)(object)left + (Byte)(object)right);
            }
            else if (typeof(T) == typeof(SByte))
            {
                return (T)(object)(SByte)((SByte)(object)left + (SByte)(object)right);
            }
            else if (typeof(T) == typeof(UInt16))
            {
                return (T)(object)(UInt16)((UInt16)(object)left + (UInt16)(object)right);
            }
            else if (typeof(T) == typeof(Int16))
            {
                return (T)(object)(Int16)((Int16)(object)left + (Int16)(object)right);
            }
            else if (typeof(T) == typeof(UInt32))
            {
                return (T)(object)(UInt32)((UInt32)(object)left + (UInt32)(object)right);
            }
            else if (typeof(T) == typeof(Int32))
            {
                return (T)(object)(Int32)((Int32)(object)left + (Int32)(object)right);
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return (T)(object)(UInt64)((UInt64)(object)left + (UInt64)(object)right);
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (T)(object)(Int64)((Int64)(object)left + (Int64)(object)right);
            }
            else if (typeof(T) == typeof(Single))
            {
                return (T)(object)(Single)((Single)(object)left + (Single)(object)right);
            }
            else if (typeof(T) == typeof(Double))
            {
                return (T)(object)(Double)((Double)(object)left + (Double)(object)right);
            }
            else
            {
                throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static T ScalarSubtract(T left, T right)
        {
            if (typeof(T) == typeof(Byte))
            {
                return (T)(object)(Byte)((Byte)(object)left - (Byte)(object)right);
            }
            else if (typeof(T) == typeof(SByte))
            {
                return (T)(object)(SByte)((SByte)(object)left - (SByte)(object)right);
            }
            else if (typeof(T) == typeof(UInt16))
            {
                return (T)(object)(UInt16)((UInt16)(object)left - (UInt16)(object)right);
            }
            else if (typeof(T) == typeof(Int16))
            {
                return (T)(object)(Int16)((Int16)(object)left - (Int16)(object)right);
            }
            else if (typeof(T) == typeof(UInt32))
            {
                return (T)(object)(UInt32)((UInt32)(object)left - (UInt32)(object)right);
            }
            else if (typeof(T) == typeof(Int32))
            {
                return (T)(object)(Int32)((Int32)(object)left - (Int32)(object)right);
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return (T)(object)(UInt64)((UInt64)(object)left - (UInt64)(object)right);
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (T)(object)(Int64)((Int64)(object)left - (Int64)(object)right);
            }
            else if (typeof(T) == typeof(Single))
            {
                return (T)(object)(Single)((Single)(object)left - (Single)(object)right);
            }
            else if (typeof(T) == typeof(Double))
            {
                return (T)(object)(Double)((Double)(object)left - (Double)(object)right);
            }
            else
            {
                throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static T ScalarMultiply(T left, T right)
        {
            if (typeof(T) == typeof(Byte))
            {
                return (T)(object)(Byte)((Byte)(object)left * (Byte)(object)right);
            }
            else if (typeof(T) == typeof(SByte))
            {
                return (T)(object)(SByte)((SByte)(object)left * (SByte)(object)right);
            }
            else if (typeof(T) == typeof(UInt16))
            {
                return (T)(object)(UInt16)((UInt16)(object)left * (UInt16)(object)right);
            }
            else if (typeof(T) == typeof(Int16))
            {
                return (T)(object)(Int16)((Int16)(object)left * (Int16)(object)right);
            }
            else if (typeof(T) == typeof(UInt32))
            {
                return (T)(object)(UInt32)((UInt32)(object)left * (UInt32)(object)right);
            }
            else if (typeof(T) == typeof(Int32))
            {
                return (T)(object)(Int32)((Int32)(object)left * (Int32)(object)right);
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return (T)(object)(UInt64)((UInt64)(object)left * (UInt64)(object)right);
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (T)(object)(Int64)((Int64)(object)left * (Int64)(object)right);
            }
            else if (typeof(T) == typeof(Single))
            {
                return (T)(object)(Single)((Single)(object)left * (Single)(object)right);
            }
            else if (typeof(T) == typeof(Double))
            {
                return (T)(object)(Double)((Double)(object)left * (Double)(object)right);
            }
            else
            {
                throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static T ScalarDivide(T left, T right)
        {
            if (typeof(T) == typeof(Byte))
            {
                return (T)(object)(Byte)((Byte)(object)left / (Byte)(object)right);
            }
            else if (typeof(T) == typeof(SByte))
            {
                return (T)(object)(SByte)((SByte)(object)left / (SByte)(object)right);
            }
            else if (typeof(T) == typeof(UInt16))
            {
                return (T)(object)(UInt16)((UInt16)(object)left / (UInt16)(object)right);
            }
            else if (typeof(T) == typeof(Int16))
            {
                return (T)(object)(Int16)((Int16)(object)left / (Int16)(object)right);
            }
            else if (typeof(T) == typeof(UInt32))
            {
                return (T)(object)(UInt32)((UInt32)(object)left / (UInt32)(object)right);
            }
            else if (typeof(T) == typeof(Int32))
            {
                return (T)(object)(Int32)((Int32)(object)left / (Int32)(object)right);
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return (T)(object)(UInt64)((UInt64)(object)left / (UInt64)(object)right);
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (T)(object)(Int64)((Int64)(object)left / (Int64)(object)right);
            }
            else if (typeof(T) == typeof(Single))
            {
                return (T)(object)(Single)((Single)(object)left / (Single)(object)right);
            }
            else if (typeof(T) == typeof(Double))
            {
                return (T)(object)(Double)((Double)(object)left / (Double)(object)right);
            }
            else
            {
                throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static T GetZeroValue()
        {
            if (typeof(T) == typeof(Byte))
            {
                Byte value = 0;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(SByte))
            {
                SByte value = 0;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(UInt16))
            {
                UInt16 value = 0;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(Int16))
            {
                Int16 value = 0;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(UInt32))
            {
                UInt32 value = 0;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(Int32))
            {
                Int32 value = 0;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(UInt64))
            {
                UInt64 value = 0;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(Int64))
            {
                Int64 value = 0;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(Single))
            {
                Single value = 0;
                return (T)(object)value;
            }
            else
            {
                if (typeof(T) != typeof(Double))
                {
                    throwNotSupported();
                }
                Double value = 0;
                return (T)(object)value;
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static T GetOneValue()
        {
            if (typeof(T) == typeof(Byte))
            {
                Byte value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(SByte))
            {
                SByte value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(UInt16))
            {
                UInt16 value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(Int16))
            {
                Int16 value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(UInt32))
            {
                UInt32 value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(Int32))
            {
                Int32 value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(UInt64))
            {
                UInt64 value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(Int64))
            {
                Int64 value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(Single))
            {
                Single value = 1;
                return (T)(object)value;
            }
            else
            {
                if (typeof(T) != typeof(Double))
                {
                    throwNotSupported();
                }
                Double value = 1;
                return (T)(object)value;
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static T GetAllBitsSetValue()
        {
            if (typeof(T) == typeof(Byte))
            {
                return (T)(object)ConstantHelper.GetByteWithAllBitsSet();
            }
            else if (typeof(T) == typeof(SByte))
            {
                return (T)(object)ConstantHelper.GetSByteWithAllBitsSet();
            }
            else if (typeof(T) == typeof(UInt16))
            {
                return (T)(object)ConstantHelper.GetUInt16WithAllBitsSet();
            }
            else if (typeof(T) == typeof(Int16))
            {
                return (T)(object)ConstantHelper.GetInt16WithAllBitsSet();
            }
            else if (typeof(T) == typeof(UInt32))
            {
                return (T)(object)ConstantHelper.GetUInt32WithAllBitsSet();
            }
            else if (typeof(T) == typeof(Int32))
            {
                return (T)(object)ConstantHelper.GetInt32WithAllBitsSet();
            }
            else if (typeof(T) == typeof(UInt64))
            {
                return (T)(object)ConstantHelper.GetUInt64WithAllBitsSet();
            }
            else if (typeof(T) == typeof(Int64))
            {
                return (T)(object)ConstantHelper.GetInt64WithAllBitsSet();
            }
            else if (typeof(T) == typeof(Single))
            {
                return (T)(object)ConstantHelper.GetSingleWithAllBitsSet();
            }
            else
            {
                if (typeof(T) != typeof(Double))
                {
                    throwNotSupported();
                }
                return (T)(object)ConstantHelper.GetDoubleWithAllBitsSet();
            }
        }
        #endregion
    }
}
