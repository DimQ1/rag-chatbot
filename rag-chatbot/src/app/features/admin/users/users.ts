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
import { AdminDocument, AdminRagConfiguration, AdminService, AdminUser } from '../../../core/services/admin/admin';

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
  ],
  templateUrl: './users.html',
  styleUrl: './users.scss',
})
export class AdminUsers implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly savingId = signal<string | null>(null);
  readonly deletingId = signal<string | null>(null);
  readonly documentsLoading = signal(false);
  readonly documentSaving = signal(false);
  readonly documentDeleting = signal(false);
  readonly documentReprocessing = signal(false);
  readonly ragConfigLoading = signal(false);
  readonly ragConfigSaving = signal(false);
  readonly errorMessage = signal('');
  readonly documentMessage = signal('');
  readonly ragConfigMessage = signal('');

  users: AdminUser[] = [];
  documents: AdminDocument[] = [];
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

  ngOnInit(): void {
    this.loadUsers();
    this.loadDocuments();
    this.loadRagConfiguration();
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
