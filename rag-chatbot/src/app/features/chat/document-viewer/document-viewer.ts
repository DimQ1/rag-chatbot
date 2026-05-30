import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RagDocumentResponse, RagService } from '../../../core/services/rag/rag';

@Component({
  selector: 'app-document-viewer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, MatToolbarModule, MatIconModule, MatButtonModule],
  templateUrl: './document-viewer.html',
  styleUrl: './document-viewer.scss',
})
export class DocumentViewer implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly ragService = inject(RagService);

  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly document = signal<RagDocumentResponse | null>(null);

  ngOnInit(): void {
    const documentId = this.route.snapshot.paramMap.get('documentId');
    if (!documentId) {
      this.errorMessage.set('Document id is missing.');
      this.loading.set(false);
      return;
    }

    this.ragService.getDocument(documentId).subscribe({
      next: (response) => {
        this.document.set(response);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message ?? 'Failed to load document.');
        this.loading.set(false);
      },
    });
  }
}
