import { useState } from 'react'
import { getBookPreviewContent } from '../../data/bookPreviewContent.js'

export function BookCatalogDetails({ book, borrowing, canBorrow, onBorrow, onPreview }) {
  const researched = getBookPreviewContent(book)
  const [coverFailed, setCoverFailed] = useState(false)
  const coverUrl = book.coverUrl || researched?.coverUrl
  const coverFromOpenLibrary = !book.coverUrl && Boolean(researched?.coverUrl)
  const about = researched?.abstract
    || book.previewAbstract
    || researched?.introduction
    || book.previewIntroduction
    || 'Book description has not been added to the catalog yet.'

  return (
    <section className="catalog-book-details">
      <div>
        <div className="catalog-book-cover" aria-label={`Cover of ${book.title}`}>
          {!coverUrl || coverFailed ? (
            <>
              <span className="catalog-cover-ornament" aria-hidden="true">✦</span>
              <span className="catalog-cover-label">Greenleaf Library</span>
              <strong>{book.title}</strong>
              <span className="catalog-cover-author">{book.authorName}</span>
            </>
          ) : (
            <img
              className="catalog-book-cover-image"
              src={coverUrl}
              alt={`Cover of ${book.title}`}
              onError={() => setCoverFailed(true)}
            />
          )}
        </div>
        {coverFromOpenLibrary && coverUrl && !coverFailed && (
          <a className="catalog-cover-source" href="https://openlibrary.org" target="_blank" rel="noreferrer">
            Cover image via Open Library
          </a>
        )}
      </div>

      <div className="catalog-book-information">
        <span className="eyebrow">About this book</span>
        <h3>{book.title}</h3>
        <p className="catalog-book-byline">{researched?.author || book.authorName}</p>
        <p className="catalog-book-about">{about}</p>

        <dl className="catalog-book-facts">
          <div><dt>Category</dt><dd>{book.categoryName}</dd></div>
          <div><dt>Published</dt><dd>{book.publishedYear}</dd></div>
          <div><dt>ISBN</dt><dd>{book.isbn}</dd></div>
          <div><dt>Availability</dt><dd>{book.availableCopies} of {book.totalCopies} copies</dd></div>
        </dl>

        <div className="catalog-book-actions">
          {canBorrow && (
            <button
              className="primary-button"
              type="button"
              disabled={book.availableCopies <= 0 || borrowing}
              onClick={onBorrow}
            >
              {borrowing ? 'Borrowing...' : 'Borrow'}
            </button>
          )}
          <button className="secondary-button" type="button" onClick={onPreview}>
            Preview book
          </button>
        </div>
      </div>
    </section>
  )
}
