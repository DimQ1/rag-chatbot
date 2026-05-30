import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { AdminDocument, AdminLogEntry, AdminLogQueryResponse, AdminRagConfiguration, AdminService, AdminUser } from '../../../core/services/admin/admin';
import { NewDocumentChoice, NewDocumentChoiceDialog } from './new-document-choice-dialog';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatDialogModule,
  ],
  templateUrl: './users.html',
  styleUrl: './users.scss',
})
export class AdminUsers implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly fb = inject(FormBuilder);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(false);
  readonly savingId = signal<string | null>(null);
  readonly deletingId = signal<string | null>(null);
  readonly documentsLoading = signal(false);
  readonly documentSaving = signal(false);
  readonly documentDeleting = signal(false);
  readonly documentReprocessing = signal(false);
  readonly ragConfigLoading = signal(false);
  readonly ragConfigSaving = signal(false);
  readonly logsLoading = signal(false);
  readonly errorMessage = signal('');
  readonly documentMessage = signal('');
  readonly ragConfigMessage = signal('');

  users: AdminUser[] = [];
  documents: AdminDocument[] = [];
  logs: AdminLogEntry[] = [];
  logsResponse: AdminLogQueryResponse | null = null;
  logsPage = 1;
  activeDocumentId: string | null = null;
  editForms: Record<string, FormGroup> = {};
  ragConfiguration: AdminRagConfiguration | null = null;
  readonly documentForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(2)]],
    content: ['', [Validators.required, Validators.minLength(10)]],
  });
  readonly ragConfigurationForm = this.fb.nonNullable.group({
    openAIBaseUrl: ['', [Validators.required, Validators.minLength(10)]],
    modelId: ['', [Validators.required, Validators.minLength(2)]],
    embeddingModelId: ['', [Validators.required, Validators.minLength(2)]],
    openAIApiKey: [''],
    topK: [3, [Validators.required, Validators.min(1), Validators.max(10)]],
  });
  readonly logSearchForm = this.fb.nonNullable.group({
    search: [''],
    level: [''],
    pageSize: [50, [Validators.required, Validators.min(10), Validators.max(200)]],
  });

  ngOnInit(): void {
    this.loadUsers();
    this.loadDocuments();
    this.loadRagConfiguration();
    this.loadLogs();
  }

  loadUsers(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.adminService.getUsers().subscribe({
      next: (users) => {
        this.users = users;
        this.editForms = {};
        for (const user of users) {
          this.editForms[user.id] = this.fb.group({
            name: [user.name, [Validators.required, Validators.minLength(2)]],
            role: [user.role, [Validators.required]],
            newPassword: ['', [Validators.minLength(6)]],
          });
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to load users.');
      },
    });
  }

  loadDocuments(): void {
    this.documentsLoading.set(true);
    this.errorMessage.set('');

    this.adminService.getDocuments().subscribe({
      next: (documents) => {
        this.documents = documents;
        this.documentsLoading.set(false);

        if (documents.length > 0) {
          const currentId = this.activeDocumentId;
          const selected = currentId
            ? documents.find((doc) => doc.id === currentId) ?? documents[0]
            : documents[0];
          this.selectDocument(selected);
        } else {
          this.startNewDocument();
        }
      },
      error: (err) => {
        this.documentsLoading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to load documents.');
      },
    });
  }

  loadRagConfiguration(): void {
    this.ragConfigLoading.set(true);
    this.errorMessage.set('');

    this.adminService.getRagConfiguration().subscribe({
      next: (configuration) => {
        this.ragConfiguration = configuration;
        this.ragConfigurationForm.reset({
          openAIBaseUrl: configuration.openAIBaseUrl,
          modelId: configuration.modelId,
          embeddingModelId: configuration.embeddingModelId,
          openAIApiKey: configuration.openAIApiKey,
          topK: configuration.topK,
        });
        this.ragConfigLoading.set(false);
      },
      error: (err) => {
        this.ragConfigLoading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to load RAG configuration.');
      },
    });
  }

  loadLogs(): void {
    if (this.logSearchForm.invalid) {
      this.logSearchForm.markAllAsTouched();
      return;
    }

    const raw = this.logSearchForm.getRawValue();
    this.logsLoading.set(true);
    this.errorMessage.set('');

    this.adminService.getLogs({
      search: raw.search.trim() || undefined,
      level: raw.level || undefined,
      page: this.logsPage,
      pageSize: raw.pageSize,
    }).subscribe({
      next: (response) => {
        this.logsResponse = response;
        this.logs = response.items;
        this.logsPage = response.page;
        this.logsLoading.set(false);
      },
      error: (err) => {
        this.logsLoading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to load logs.');
      },
    });
  }

  save(user: AdminUser): void {
    const form = this.editForms[user.id];
    if (!form || form.invalid) {
      form?.markAllAsTouched();
      return;
    }

    this.savingId.set(user.id);
    this.errorMessage.set('');

    const payload = form.getRawValue() as { name: string; role: 'User' | 'Admin'; newPassword: string };
    this.adminService.updateUser(user.id, {
      name: payload.name,
      role: payload.role,
      newPassword: payload.newPassword ? payload.newPassword : undefined,
    }).subscribe({
      next: (updated) => {
        this.users = this.users.map((u) => (u.id === updated.id ? updated : u));
        this.editForms[user.id].patchValue({ newPassword: '' });
        this.savingId.set(null);
      },
      error: (err) => {
        this.savingId.set(null);
        this.errorMessage.set(err?.error?.message ?? 'Failed to update user.');
      },
    });
  }

  delete(user: AdminUser): void {
    if (!confirm(`Delete user ${user.email}?`)) {
      return;
    }

    this.deletingId.set(user.id);
    this.errorMessage.set('');

    this.adminService.deleteUser(user.id).subscribe({
      next: () => {
        this.users = this.users.filter((u) => u.id !== user.id);
        delete this.editForms[user.id];
        this.deletingId.set(null);
      },
      error: (err) => {
        this.deletingId.set(null);
        this.errorMessage.set(err?.error?.message ?? 'Failed to delete user.');
      },
    });
  }

  selectDocument(document: AdminDocument): void {
    this.activeDocumentId = document.id;
    this.documentMessage.set('');
    this.documentForm.reset({
      title: document.title,
      content: document.content,
    });
  }

  startNewDocument(): void {
    this.activeDocumentId = null;
    this.documentMessage.set('');
    this.documentForm.reset({
      title: '',
      content: '',
    });
  }

  requestNewDocument(fileInput: HTMLInputElement): void {
    const dialogRef = this.dialog.open(NewDocumentChoiceDialog, {
      width: '420px',
      maxWidth: '95vw',
      disableClose: false,
      autoFocus: false,
    });

    dialogRef.afterClosed().subscribe((choice: NewDocumentChoice | undefined) => {
      if (choice === 'form') {
        this.createDocumentFromForm();
        return;
      }

      if (choice === 'file') {
        this.triggerTextFilePicker(fileInput);
      }
    });
  }

  createDocumentFromForm(): void {
    this.activeDocumentId = null;
    this.documentMessage.set('Ready to create a new document manually.');
    this.documentForm.reset({
      title: '',
      content: '',
    });
  }

  triggerTextFilePicker(fileInput: HTMLInputElement): void {
    fileInput.value = '';
    fileInput.click();
  }

  async onTextFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0];
    if (!file) {
      return;
    }

    try {
      const rawText = await file.text();
      const analyzed = this.analyzeUploadedDocument(file.name, rawText);

      this.activeDocumentId = null;
      this.documentForm.reset({
        title: analyzed.title,
        content: analyzed.content,
      });
      this.documentMessage.set('Text file imported. Review and save the new document.');
    } catch {
      this.errorMessage.set('Failed to load the selected file.');
    }
  }

  saveDocument(): void {
    if (this.documentForm.invalid) {
      this.documentForm.markAllAsTouched();
      return;
    }

    this.documentSaving.set(true);
    this.errorMessage.set('');
    this.documentMessage.set('');

    const payload = this.documentForm.getRawValue();
    const request = this.activeDocumentId
      ? this.adminService.updateDocument(this.activeDocumentId, payload)
      : this.adminService.createDocument(payload);

    request.subscribe({
      next: (document) => {
        const existingIndex = this.documents.findIndex((item) => item.id === document.id);
        if (existingIndex >= 0) {
          this.documents = this.documents.map((item) => item.id === document.id ? document : item);
        } else {
          this.documents = [document, ...this.documents];
        }

        this.activeDocumentId = document.id;
        this.documentForm.reset({
          title: document.title,
          content: document.content,
        });
        this.documentSaving.set(false);
        this.documentMessage.set('Document saved.');
        this.reprocessDocument(document.id);
      },
      error: (err) => {
        this.documentSaving.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to save document.');
      },
    });
  }

  deleteDocument(): void {
    if (!this.activeDocumentId) {
      return;
    }

    const activeDocument = this.documents.find((document) => document.id === this.activeDocumentId);
    if (activeDocument && !confirm(`Delete document ${activeDocument.title}?`)) {
      return;
    }

    this.documentDeleting.set(true);
    this.errorMessage.set('');
    this.documentMessage.set('');

    this.adminService.deleteDocument(this.activeDocumentId).subscribe({
      next: () => {
        this.documents = this.documents.filter((document) => document.id !== this.activeDocumentId);
        this.documentDeleting.set(false);
        this.documentMessage.set('Document deleted.');
        if (this.documents.length > 0) {
          this.selectDocument(this.documents[0]);
        } else {
          this.startNewDocument();
        }
      },
      error: (err) => {
        this.documentDeleting.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to delete document.');
      },
    });
  }

  reprocessAllDocuments(): void {
    this.documentReprocessing.set(true);
    this.errorMessage.set('');
    this.documentMessage.set('');

    this.adminService.reprocessAllDocuments().subscribe({
      next: (result) => {
        this.documentReprocessing.set(false);
        this.documentMessage.set(`Vector DB refreshed. Processed: ${result.processedCount}, Removed: ${result.removedCount}.`);
      },
      error: (err) => {
        this.documentReprocessing.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to reprocess documents.');
      },
    });
  }

  searchLogs(): void {
    this.logsPage = 1;
    this.loadLogs();
  }

  clearLogFilters(): void {
    this.logsPage = 1;
    this.logSearchForm.reset({
      search: '',
      level: '',
      pageSize: 50,
    });
    this.loadLogs();
  }

  previousLogsPage(): void {
    if (this.logsPage <= 1) {
      return;
    }

    this.logsPage -= 1;
    this.loadLogs();
  }

  nextLogsPage(): void {
    if (this.logsPage >= this.logsTotalPages) {
      return;
    }

    this.logsPage += 1;
    this.loadLogs();
  }

  get logsTotalCount(): number {
    return this.logsResponse?.totalCount ?? 0;
  }

  get logsTotalPages(): number {
    if (!this.logsResponse) {
      return 1;
    }

    return Math.max(1, Math.ceil(this.logsResponse.totalCount / this.logsResponse.pageSize));
  }

  reprocessActiveDocument(): void {
    if (!this.activeDocumentId) {
      return;
    }

    this.reprocessDocument(this.activeDocumentId);
  }

  private reprocessDocument(documentId: string): void {
    this.documentReprocessing.set(true);
    this.errorMessage.set('');

    this.adminService.reprocessDocument(documentId).subscribe({
      next: (result) => {
        this.documentReprocessing.set(false);
        this.documentMessage.set(result.message);
      },
      error: (err) => {
        this.documentReprocessing.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to reprocess document.');
      },
    });
  }

  private analyzeUploadedDocument(fileName: string, rawText: string): { title: string; content: string } {
    const normalizedText = rawText.replace(/\r\n/g, '\n').trim();
    const fallbackTitle = this.createTitleFromFileName(fileName);

    if (!normalizedText) {
      return {
        title: fallbackTitle,
        content: `# ${fallbackTitle}\n\n`,
      };
    }

    const lines = normalizedText.split('\n');
    const headingLine = lines.find((line) => line.trim().startsWith('# '));
    if (headingLine) {
      const detectedTitle = headingLine.trim().replace(/^#\s+/, '').trim();
      const safeTitle = detectedTitle || fallbackTitle;
      const withTitleHeading = this.ensureMarkdownTitle(safeTitle, normalizedText);
      return {
        title: safeTitle,
        content: withTitleHeading,
      };
    }

    const firstContentLine = lines.find((line) => line.trim().length > 0) ?? '';
    const inferredTitle = this.inferTitle(firstContentLine, fallbackTitle);
    return {
      title: inferredTitle,
      content: this.ensureMarkdownTitle(inferredTitle, normalizedText),
    };
  }

  private createTitleFromFileName(fileName: string): string {
    const withoutExtension = fileName.replace(/\.[^/.]+$/, '');
    const words = withoutExtension
      .replace(/[_-]+/g, ' ')
      .trim();

    if (!words) {
      return 'New Document';
    }

    return words
      .split(/\s+/)
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ')
      .trim();
  }

  private inferTitle(candidateLine: string, fallbackTitle: string): string {
    const normalized = candidateLine
      .replace(/^\d+[.)]\s+/, '')
      .replace(/[\t]+/g, ' ')
      .trim();

    if (!normalized) {
      return fallbackTitle;
    }

    const shortEnough = normalized.length <= 80;
    const sentenceLike = /[.!?]$/.test(normalized);

    if (shortEnough && !sentenceLike) {
      return normalized;
    }

    return fallbackTitle;
  }

  private ensureMarkdownTitle(title: string, content: string): string {
    const trimmed = content.trim();
    const heading = `# ${title.trim()}`;

    if (trimmed.startsWith('# ')) {
      const lines = trimmed.split('\n');
      lines[0] = heading;
      return `${lines.join('\n').trim()}\n`;
    }

    return `${heading}\n\n${trimmed}\n`;
  }

  saveRagConfiguration(): void {
    if (this.ragConfigurationForm.invalid) {
      this.ragConfigurationForm.markAllAsTouched();
      return;
    }

    this.ragConfigSaving.set(true);
    this.errorMessage.set('');
    this.ragConfigMessage.set('');

    this.adminService.updateRagConfiguration(this.ragConfigurationForm.getRawValue()).subscribe({
      next: (configuration) => {
        this.ragConfiguration = configuration;
        this.ragConfigurationForm.reset({
          openAIBaseUrl: configuration.openAIBaseUrl,
          modelId: configuration.modelId,
          embeddingModelId: configuration.embeddingModelId,
          openAIApiKey: configuration.openAIApiKey,
          topK: configuration.topK,
        });
        this.ragConfigSaving.set(false);
        this.ragConfigMessage.set('RAG configuration saved.');
      },
      error: (err) => {
        this.ragConfigSaving.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to save RAG configuration.');
      },
    });
  }
}
