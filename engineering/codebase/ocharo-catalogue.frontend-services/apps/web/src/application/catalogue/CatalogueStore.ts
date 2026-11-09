import { ref } from 'vue';
import { notifyError } from '../common/UseFeedback';

// Local interface state shared by the frame and its pages; server values live in the query cache.
const error = ref<string | null>(null);
const notice = ref<string | null>(null);

/** The frame's shared feedback: the last failure, reported through the toast host, and the success notice. */
export function useCatalogueStore() {
  return {
    /** The last failure's interface copy, already reported through the toast host. */
    error,
    /** The last success, shown above the open place until dismissed. */
    notice,
    fail(message: string) {
      notice.value = null;
      error.value = message;
      notifyError(message);
    },
    succeed(message: string) {
      error.value = null;
      notice.value = message;
    },
  };
}
