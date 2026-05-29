import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DocumentViewer } from './document-viewer';
import { RagService } from '../../../core/services/rag';

describe('DocumentViewer', () => {
  let component: DocumentViewer;
  let fixture: ComponentFixture<DocumentViewer>;
  let ragServiceMock: {
    getDocument: ReturnType<typeof vi.fn>;
  };

  const setup = async (documentId: string | null): Promise<void> => {
    ragServiceMock = {
      getDocument: vi.fn().mockReturnValue(
        of({
          documentId: 'doc-1',
          title: 'Test document',
          content: 'Body',
          sourceUpdatedAtUtc: '2026-01-01T00:00:00Z',
        })
      ),
    };

    await TestBed.configureTestingModule({
      imports: [DocumentViewer],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap(
                documentId ? { documentId } : {}
              ),
            },
          },
        },
        {
          provide: RagService,
          useValue: ragServiceMock,
        },
      ],
    })
      .overrideComponent(DocumentViewer, {
        set: {
          template: '',
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(DocumentViewer);
    component = fixture.componentInstance;
  };

  it('should create', async () => {
    await setup('doc-1');
    expect(component).toBeTruthy();
  });

  it('should set missing document id error', async () => {
    await setup(null);

    component.ngOnInit();

    expect(component.loading()).toBe(false);
    expect(component.errorMessage()).toBe('Document id is missing.');
    expect(ragServiceMock.getDocument).not.toHaveBeenCalled();
  });

  it('should load document by id', async () => {
    await setup('doc-1');

    component.ngOnInit();

    expect(ragServiceMock.getDocument).toHaveBeenCalledWith('doc-1');
    expect(component.loading()).toBe(false);
    expect(component.document()?.title).toBe('Test document');
    expect(component.errorMessage()).toBe('');
  });

  it('should set error message when load fails', async () => {
    await setup('doc-1');
    ragServiceMock.getDocument.mockReturnValue(
      throwError(() => ({ error: { message: 'Document unavailable.' } }))
    );

    component.ngOnInit();

    expect(component.loading()).toBe(false);
    expect(component.errorMessage()).toBe('Document unavailable.');
  });
});
