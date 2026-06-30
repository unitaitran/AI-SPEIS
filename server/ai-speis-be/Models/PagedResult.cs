using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ai_speis_be.Models
{
    public class PagedResult<T>
    {
       public required IReadOnlyList<T> Items { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public int TotalItems { get; init; }
        public int TotalPages => TotalItems == 0
            ? 0
            : (int)Math.Ceiling(TotalItems / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}