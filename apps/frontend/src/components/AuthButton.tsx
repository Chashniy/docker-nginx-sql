export default function AuthButton() {
  const isAuthenticated = false;

  return (
    <a href={isAuthenticated ? '/admin' : '/login'} className="auth-button">
      {isAuthenticated ? 'Админка' : 'Войти'}
    </a>
  );
}