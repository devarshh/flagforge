type Listener = (...args: never[]) => void;

/** A minimal typed event emitter. A throwing listener is reported and does not stop the others. */
export class Emitter<Events extends { [E in keyof Events]: Listener }> {
  private readonly listeners = new Map<keyof Events, Set<Events[keyof Events]>>();

  constructor(private readonly onListenerError: (error: unknown) => void) {}

  on<E extends keyof Events>(event: E, listener: Events[E]): () => void {
    let set = this.listeners.get(event);
    if (!set) {
      set = new Set();
      this.listeners.set(event, set);
    }

    set.add(listener);
    return () => {
      set.delete(listener);
    };
  }

  emit<E extends keyof Events>(event: E, ...args: Parameters<Events[E]>): void {
    for (const listener of [...(this.listeners.get(event) ?? [])]) {
      try {
        (listener as (...values: Parameters<Events[E]>) => void)(...args);
      } catch (error) {
        this.onListenerError(error);
      }
    }
  }

  clear(): void {
    this.listeners.clear();
  }
}
