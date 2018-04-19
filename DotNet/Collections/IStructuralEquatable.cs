// Copyright (c) Microsoft. All rights reserved.

#if NET_2_0 || NET_2_0_SUBSET

namespace System.Collections
{
    public interface IStructuralEquatable
	{
        Boolean Equals(Object other, IEqualityComparer comparer);
        int GetHashCode(IEqualityComparer comparer);
    }
}

#endif
