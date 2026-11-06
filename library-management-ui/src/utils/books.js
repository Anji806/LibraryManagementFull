// Client-side catalog search: every space-separated term must match the title,
// author, category, published year or ISBN (case-insensitive).
export function filterBooks(books, query) {
  const terms = query.toLowerCase().split(/\s+/).filter(Boolean)
  if (terms.length === 0) return books

  return books.filter((book) => {
    const haystack = [book.title, book.authorName, book.categoryName, book.publishedYear, book.isbn]
      .filter((value) => value !== null && value !== undefined)
      .join(' ')
      .toLowerCase()
    return terms.every((term) => haystack.includes(term))
  })
}
