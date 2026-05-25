import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import mermaid from 'mermaid';
import { vi } from 'vitest';
import { MarkdownContentDirective } from './markdown-content';

vi.mock('mermaid', () => ({
  default: {
    initialize: vi.fn(),
    render: vi.fn().mockResolvedValue({
      svg: '<svg viewBox="0 0 10 10"><title>Diagram</title><path d="M0 0h10v10H0z"></path></svg>',
    }),
  },
}));

@Component({
  imports: [MarkdownContentDirective],
  template: '<div class="content" [appMarkdownContent]="message"></div>',
})
class MarkdownHostComponent {
  message = '';
}

describe('MarkdownContentDirective', () => {
  let fixture: ComponentFixture<MarkdownHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MarkdownHostComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(MarkdownHostComponent);
  });

  it('renders multiline GitHub-flavored Markdown', async () => {
    fixture.componentInstance.message = 'First line\nSecond line\n\n- One\n- Two';
    fixture.detectChanges();
    await fixture.whenStable();

    const content = getContent();

    expect(content.querySelectorAll('p').length).toBeGreaterThan(0);
    expect(content.querySelectorAll('li')).toHaveLength(2);
    expect(content.innerHTML).toContain('<br>');
  });

  it('sanitizes unsafe raw HTML from Markdown', async () => {
    fixture.componentInstance.message = '<img src="x" onerror="alert(1)">';
    fixture.detectChanges();
    await fixture.whenStable();

    const image = getContent().querySelector('img');

    expect(image).not.toBeNull();
    expect(image?.hasAttribute('onerror')).toBe(false);
  });

  it('removes raw Mermaid error markup before rendering Markdown content', async () => {
    fixture.componentInstance.message =
      '<div id="dmermaid-1779697265962-1-0"><svg aria-roledescription="error"><text class="error-text">Syntax error in text</text></svg></div>\n\nVisible text';
    fixture.detectChanges();
    await fixture.whenStable();

    const content = getContent();

    expect(content.querySelector('[id^="dmermaid-"]')).toBeNull();
    expect(content.innerHTML).not.toContain('dmermaid-1779697265962-1-0');
    expect(content.textContent).not.toContain('Syntax error in text');
    expect(content.textContent).toContain('Visible text');
  });

  it('renders Mermaid code fences as sanitized SVG diagrams', async () => {
    fixture.componentInstance.message = '```mermaid\nflowchart TD\n  A[Ask] --> B[Answer]\n```';
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();

    const content = getContent();

    expect(mermaid.render).toHaveBeenCalledWith(expect.stringMatching(/^mermaid-/), 'flowchart TD\n  A[Ask] --> B[Answer]');
    expect(content.querySelector('.mermaid-diagram svg')).not.toBeNull();
    expect(content.querySelector('.mermaid-diagram')?.getAttribute('role')).toBe('img');
  });

  it('normalizes malformed mermaidgraph fences and renders diagram', async () => {
    fixture.componentInstance.message = '```mermaidgraph TD\n  A[Ask] --> B[Answer]\n```';
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();

    const content = getContent();

    expect(mermaid.render).toHaveBeenCalledWith(expect.stringMatching(/^mermaid-/), 'graph TD\n  A[Ask] --> B[Answer]');
    expect(content.querySelector('.mermaid-diagram svg')).not.toBeNull();
  });

  it('renders raw mermaid graph text without fences', async () => {
    fixture.componentInstance.message = 'graph TD; A[Start] --> B[End]';
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();

    const content = getContent();

    expect(mermaid.render).toHaveBeenCalledWith(expect.stringMatching(/^mermaid-/), 'graph TD; A[Start] --> B[End]');
    expect(content.querySelector('.mermaid-diagram svg')).not.toBeNull();
  });

  it('renders long code lines for the chat answer-card styles to wrap', async () => {
    fixture.componentInstance.message = '```text\n' + 'a'.repeat(200) + '\n```';
    fixture.detectChanges();
    await fixture.whenStable();

    const pre = getContent().querySelector('pre') as HTMLElement;
    const code = getContent().querySelector('pre code') as HTMLElement;

    expect(pre).not.toBeNull();
    expect(code.textContent).toContain('a'.repeat(200));
  });

  it('logs Mermaid render issues and keeps only chat content visible on parse failures', async () => {
    vi.mocked(mermaid.render).mockRejectedValueOnce(new Error('Syntax error in graph'));
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

    fixture.componentInstance.message =
      '```mermaid\nmermaidgraph TD\n  A[Broken]\n```\n\nAdditional text';
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();

    const content = getContent();
    const fallbackText = content.querySelector('.mermaid-diagram-error')?.textContent ?? '';

    expect(content.querySelector('.mermaid-diagram-error')).not.toBeNull();
    expect(fallbackText).not.toContain('Mermaid diagram could not be rendered.');
    expect(fallbackText).not.toContain('Syntax error in graph');
    expect(fallbackText).toContain('mermaidgraph TD');
    expect(content.textContent).toContain('Additional text');
    expect(warnSpy).toHaveBeenCalledWith('Mermaid diagram could not be rendered.', {
      error: 'Syntax error in graph',
      diagramSource: 'mermaidgraph TD\n  A[Broken]',
      rawContent: '```mermaid\nmermaidgraph TD\n  A[Broken]\n```\n\nAdditional text',
    });

    warnSpy.mockRestore();
  });

  it('replaces Mermaid error SVGs with raw answer fallback and logs issue to console', async () => {
    vi.mocked(mermaid.render).mockResolvedValueOnce({
      svg: '<svg aria-roledescription="error"><text class="error-text">Syntax error in text</text><text class="error-text">mermaid version 11.15.0</text></svg>',
      diagramType: 'error',
    });
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

    fixture.componentInstance.message = '```mermaid\ngraph TD; A[Broken label --> B[End]\n```';
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();

    const content = getContent();
    const fallbackText = content.querySelector('.mermaid-diagram-error')?.textContent ?? '';

    expect(content.querySelector('.mermaid-diagram-error')).not.toBeNull();
    expect(content.querySelector('svg')).toBeNull();
    expect(fallbackText).not.toContain('Mermaid diagram could not be rendered.');
    expect(fallbackText).not.toContain('Raw Mermaid block');
    expect(fallbackText).not.toContain('Raw answer');
    expect(fallbackText).toContain('graph TD; A[Broken label --> B[End]');
    expect(warnSpy).toHaveBeenCalledWith('Mermaid diagram could not be rendered.', {
      error: 'Syntax error in text | mermaid version 11.15.0',
      diagramSource: 'graph TD; A[Broken label --> B[End]',
      rawContent: '```mermaid\ngraph TD; A[Broken label --> B[End]\n```',
    });

    warnSpy.mockRestore();
  });

  it('removes Mermaid body-level error wrappers left behind during failed renders', async () => {
    vi.mocked(mermaid.render).mockImplementationOnce(async (renderId: string) => {
      const wrapper = document.createElement('div');
      wrapper.id = `d${renderId}`;
      wrapper.innerHTML =
        '<svg aria-roledescription="error"><text class="error-text">Syntax error in text</text></svg>';
      document.body.appendChild(wrapper);

      return {
        svg: '<svg aria-roledescription="error"><text class="error-text">Syntax error in text</text></svg>',
        diagramType: 'error',
      };
    });

    fixture.componentInstance.message = '```mermaid\ngraph TD; A[Broken label --> B[End]\n```';
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();

    expect(document.querySelector('[id^="dmermaid-"]')).toBeNull();
    expect(document.querySelector('svg[aria-roledescription="error"]')).toBeNull();
  });

  it('removes generated dmermaid error wrappers from the answer card UI', async () => {
    vi.mocked(mermaid.render).mockResolvedValueOnce({
      svg: '<div id="dmermaid-1779696911408-1-0"><svg id="mermaid-1779696911408-1-0" aria-roledescription="error"><text class="error-text">Syntax error in text</text></svg></div>',
      diagramType: 'error',
    });
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

    fixture.componentInstance.message = '```mermaid\ngraph TD; A[Broken label --> B[End]\n```';
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();

    const content = getContent();
    const fallbackText = content.querySelector('.mermaid-diagram-error')?.textContent ?? '';

    expect(content.querySelector('[id^="dmermaid-"]')).toBeNull();
    expect(content.innerHTML).not.toContain('dmermaid-1779696911408-1-0');
    expect(fallbackText).not.toContain('dmermaid-1779696911408-1-0');
    expect(fallbackText).not.toContain('Raw Mermaid block');
    expect(fallbackText).toContain('graph TD; A[Broken label --> B[End]');
    expect(warnSpy).toHaveBeenCalledWith('Mermaid diagram could not be rendered.', {
      error: 'Syntax error in text',
      diagramSource: 'graph TD; A[Broken label --> B[End]',
      rawContent: '```mermaid\ngraph TD; A[Broken label --> B[End]\n```',
    });

    warnSpy.mockRestore();
  });

  function getContent(): HTMLElement {
    return fixture.nativeElement.querySelector('.content') as HTMLElement;
  }
});