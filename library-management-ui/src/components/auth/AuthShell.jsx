// Page frame shared by the password-recovery screens: the same two-panel look
// as the sign-in and register pages.
export function AuthShell({ eyebrow, title, intro, children }) {
  return (
    <main className="auth-shell">
      <aside className="auth-aside">
        <div className="brand">
          <span>Greenleaf Library</span>
        </div>
        <div className="auth-aside-copy">
          <img className="auth-hero-logo" src="/Library_icon.png" alt="Greenleaf Library" width="220" height="220" />
          <p className="eyebrow">A fresh start</p>
          <h1>Back to your books in a moment.</h1>
          <p>We will help you get back into your library account.</p>
        </div>
        <p className="auth-footer">Library Management System</p>
      </aside>
      <section className="auth-panel">
        <div className="auth-form-wrap">
          <p className="auth-eyebrow">{eyebrow}</p>
          <h2>{title}</h2>
          {intro && <p className="auth-intro">{intro}</p>}
          {children}
        </div>
      </section>
    </main>
  )
}
