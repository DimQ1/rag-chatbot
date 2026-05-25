import { Directive, ElementRef, SecurityContext, effect, inject, input } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import DOMPurify from 'dompurify';
import { marked } from 'marked';
import mermaid from 'mermaid';

interface MermaidDiagram {
  index: number;
  source: string;
}

@Directive({
  selector: '[appMarkdownContent]',
})
export class MarkdownContentDirective {
  readonly content = input('', { alias: 'appMarkdownContent' });

  private static mermaidInitialized = false;
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly sanitizer = inject(DomSanitizer);
  private renderVersion = 0;

  constructor() {
    this.initializeMermaid();

    effect(() => {
      void this.render(this.content());
    });
  }

  private async render(content: string): Promise<void> {
    const version = ++this.renderVersion;
    const diagrams: MermaidDiagram[] = [];
    const cleanedContent = this.removeGeneratedMermaidErrorMarkup(content);
    const markdown = this.replaceMermaidBlocks(cleanedContent, diagrams);
    const parsed = marked.parse(markdown, {
      async: false,
      breaks: true,
      gfm: true,
    });
    const html = typeof parsed === 'string' ? parsed : markdown;
    const sanitized = this.sanitizeHtml(html);

    this.elementRef.nativeElement.innerHTML = sanitized;

    if (diagrams.length === 0) {
      return;
    }

    await this.renderDiagrams(diagrams, version, content);
  }

  private replaceMermaidBlocks(content: string, diagrams: MermaidDiagram[]): string {
    const normalizedRaw = this.normalizeRawMermaidContent(content);
    const normalized = this.normalizeMermaidFenceSyntax(normalizedRaw);

    return normalized.replace(/```mermaid[ \t]*\r?\n([\s\S]*?)```/gi, (_, source: string) => {
      const index = diagrams.length;
      diagrams.push({ index, source: source.trim() });

      return `<div class="mermaid-diagram mermaid-diagram-index-${index}"><pre><code>${this.escapeHtml(source.trim())}</code></pre></div>`;
    });
  }

  private normalizeRawMermaidContent(content: string): string {
    if (content.includes('```')) {
      return content;
    }

    const trimmed = content.trim();
    if (!trimmed) {
      return content;
    }

    const startsLikeMermaid =
      /^(?:mermaid\s+)?(?:graph|flowchart|sequenceDiagram|classDiagram|stateDiagram(?:-v2)?|erDiagram|journey|gantt|pie|mindmap|timeline|gitGraph|quadrantChart|xychart-beta|requirementDiagram|block-beta|architecture-beta|packet-beta|sankey-beta|C4Context|C4Container|C4Component|C4Dynamic|C4Deployment)\b/i.test(
        trimmed,
      );

    const hasDiagramLinks = /-->/.test(trimmed);
    if (!startsLikeMermaid || !hasDiagramLinks) {
      return content;
    }

    const normalizedSource = trimmed.replace(/^mermaid\s+/i, '');
    return `\`\`\`mermaid\n${normalizedSource}\n\`\`\``;
  }

  private normalizeMermaidFenceSyntax(content: string): string {
    const diagramKeywords =
      '(?:graph|flowchart|sequenceDiagram|classDiagram|stateDiagram(?:-v2)?|erDiagram|journey|gantt|pie|mindmap|timeline|gitGraph|quadrantChart|xychart-beta|requirementDiagram|block-beta|architecture-beta|packet-beta|sankey-beta|C4Context|C4Container|C4Component|C4Dynamic|C4Deployment)';
    const inlineAfterSpacePattern = new RegExp('```mermaid[ \\t]+(?=' + diagramKeywords + '\\b)', 'gi');
    const inlineConcatenatedPattern = new RegExp('```mermaid(?=' + diagramKeywords + '\\b)', 'gi');

    return content
      .replace(inlineAfterSpacePattern, '```mermaid\n')
      .replace(inlineConcatenatedPattern, '```mermaid\n');
  }

  private async renderDiagrams(
    diagrams: MermaidDiagram[],
    version: number,
    rawContent: string,
  ): Promise<void> {
    for (const diagram of diagrams) {
      if (version !== this.renderVersion) {
        return;
      }

      const container = this.elementRef.nativeElement.querySelector<HTMLElement>(
        `.mermaid-diagram-index-${diagram.index}`,
      );

      if (!container) {
        continue;
      }

      const renderId = `mermaid-${Date.now()}-${version}-${diagram.index}`;

      try {
        const { svg } = await mermaid.render(renderId, diagram.source);

        if (version !== this.renderVersion) {
          return;
        }

        if (this.isMermaidErrorSvg(svg)) {
          container.classList.add('mermaid-diagram-error');
          const errorMessage = this.getMermaidErrorSvgMessage(svg);
          this.logMermaidRenderIssue(errorMessage, diagram.source, rawContent);
          container.innerHTML = this.buildMermaidErrorHtml(diagram.source, rawContent);
          continue;
        }

        // Mermaid is configured with strict security mode; re-sanitizing the
        // generated SVG can strip required style nodes and hide the diagram.
        container.innerHTML = svg;
        container.classList.remove('mermaid-diagram-error');
        container.setAttribute('role', 'img');
        container.setAttribute('aria-label', 'Markdown diagram');
      } catch (error) {
        container.classList.add('mermaid-diagram-error');
        const errorMessage = this.getErrorMessage(error);
        this.logMermaidRenderIssue(errorMessage, diagram.source, rawContent);
        container.innerHTML = this.buildMermaidErrorHtml(diagram.source, rawContent);
      } finally {
        this.cleanupMermaidRenderArtifacts(renderId);
      }
    }
  }

  private isMermaidErrorSvg(svg: string): boolean {
    return /id=["']dmermaid-[^"']*["']|aria-roledescription=["']error["']|class=["'][^"']*error-text[^"']*["']/i.test(svg);
  }

  private getMermaidErrorSvgMessage(svg: string): string {
    const textMatches = [...svg.matchAll(/<text\b[^>]*class=["'][^"']*error-text[^"']*["'][^>]*>([\s\S]*?)<\/text>/gi)];
    const message = textMatches
      .map((match) => this.decodeHtml(match[1]).trim())
      .filter(Boolean)
      .join(' | ');

    return message || 'Invalid Mermaid syntax or unsupported diagram content.';
  }

  private logMermaidRenderIssue(error: string, diagramSource: string, rawContent: string): void {
    console.warn('Mermaid diagram could not be rendered.', {
      error,
      diagramSource,
      rawContent,
    });
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof Error && error.message.trim().length > 0) {
      return error.message;
    }

    return 'Invalid Mermaid syntax or unsupported diagram content.';
  }

  private buildMermaidErrorHtml(diagramSource: string, rawContent: string): string {
    const safeDiagramSource = this.removeGeneratedMermaidErrorMarkup(diagramSource);

    return `<pre><code>${this.escapeHtml(safeDiagramSource)}</code></pre>`;
  }

  private removeGeneratedMermaidErrorMarkup(value: string): string {
    return value
      .replace(/<div\b[^>]*\bid=["']dmermaid-[^"']*["'][\s\S]*?<\/div>/gi, '')
      .replace(/<svg\b[^>]*\baria-roledescription=["']error["'][\s\S]*?<\/svg>/gi, '')
      .trim();
  }

  private cleanupMermaidRenderArtifacts(renderId: string): void {
    document.getElementById(`d${renderId}`)?.remove();
  }

  private sanitizeHtml(html: string): string {
    const purified = DOMPurify.sanitize(html, {
      USE_PROFILES: { html: true },
    });

    return this.sanitizer.sanitize(SecurityContext.HTML, purified) ?? '';
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  private decodeHtml(value: string): string {
    const element = document.createElement('textarea');
    element.innerHTML = value;
    return element.value;
  }

  private initializeMermaid(): void {
    if (MarkdownContentDirective.mermaidInitialized) {
      return;
    }

    mermaid.initialize({
      startOnLoad: false,
      securityLevel: 'strict',
      theme: 'base',
    });
    MarkdownContentDirective.mermaidInitialized = true;
  }
}