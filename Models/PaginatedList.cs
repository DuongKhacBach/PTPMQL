using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Models
{
    public class PaginatedList<T> : List<T>
    {
        public int PageIndex { get; private set; }
        public int TotalPages { get; private set; }
        public int TotalItems { get; private set; }
        public int PageSize { get; private set; }

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            TotalItems = count;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            PageIndex = pageIndex;

            this.AddRange(items);
        }

        public bool HasPreviousPage => PageIndex > 1;

        public bool HasNextPage => PageIndex < TotalPages;

        public int StartItemIndex => TotalItems == 0 ? 0 : (PageIndex - 1) * PageSize + 1;

        public int EndItemIndex => Math.Min(PageIndex * PageSize, TotalItems);

        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
        {
            if (pageSize < 1) pageSize = 5;
            var count = await source.CountAsync();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            if (pageIndex < 1) pageIndex = 1;
            if (totalPages > 0 && pageIndex > totalPages) pageIndex = totalPages;

            var items = await source
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }

        public static PaginatedList<T> Create(IEnumerable<T> source, int pageIndex, int pageSize)
        {
            if (pageSize < 1) pageSize = 5;
            var list = source as IList<T> ?? source.ToList();
            var count = list.Count;
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);

            if (pageIndex < 1) pageIndex = 1;
            if (totalPages > 0 && pageIndex > totalPages) pageIndex = totalPages;

            var items = list
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }
    }
}

