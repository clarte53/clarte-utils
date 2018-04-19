// Copyright (c) Microsoft. All rights reserved.

#if NET_2_0 || NET_2_0_SUBSET

namespace System.Collections
{
    public interface IStructuralComparable
	{
        Int32 CompareTo(Object other, IComparer comparer);
    }
}

#endif
