import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import DOMPurify from 'dompurify';
import { marked } from 'marked';

marked.setOptions({ gfm: true, breaks: true });

// Links in user Markdown open in a new tab without leaking the opener.
DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A' && node.getAttribute('href')) {
    node.setAttribute('target', '_blank');
    node.setAttribute('rel', 'noopener noreferrer nofollow');
  }
});

/** Renders Markdown, sanitized with DOMPurify (decision D-39). Raw HTML, scripts, event handlers
 * and javascript: URLs are removed; Angular sanitizes the result again. */
export function renderMarkdown(text: string | null | undefined): string {
  if (!text) return '';
  const html = marked.parse(text, { async: false });
  return DOMPurify.sanitize(html, {
    FORBID_TAGS: ['style', 'iframe', 'form', 'input', 'img'],
    FORBID_ATTR: ['style'],
  });
}

@Component({
  selector: 'app-markdown',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="prose" [innerHTML]="html()"></div>`,
})
export class MarkdownView {
  readonly text = input<string | null | undefined>();
  protected readonly html = computed(() => renderMarkdown(this.text()));
}
