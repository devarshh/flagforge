import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

const container = document.getElementById('root');
if (container) {
  createRoot(container).render(
    <StrictMode>
      <h1>FlagForge</h1>
    </StrictMode>,
  );
}
