import Clock from './components/Clock';
import SaveButton from './components/SaveButton';
import StatusIndicator from './components/StatusIndicator';

export default function App() {
  return (
    <main>
      <h1>Distributed Clock</h1>
      <Clock />
      <SaveButton />
      <StatusIndicator />
    </main>
  );
}