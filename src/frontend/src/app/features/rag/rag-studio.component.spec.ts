// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { RagStudioComponent } from './rag-studio.component';

describe('RagStudioComponent', () => {
  let component: RagStudioComponent;

  beforeEach(() => {
    component = new RagStudioComponent();
  });

  it('should initialize with default document and sample ranked chunk', () => {
    expect(component).toBeTruthy();
    expect(component.documentTitle).toBe('WhatsApp Cloud API Production Guide');
    expect(component.retrievedChunks().length).toBe(1);
    expect(component.retrievedChunks()[0].cosineSim).toBeGreaterThan(0.9);
  });

  it('should execute reciprocal rank fusion search and update ranked chunks', () => {
    component.searchQuery = 'Explain pgvector HNSW indexing';
    component.documentContent = 'HNSW indexes on pgvector provide logarithmic approximate nearest neighbor queries with high recall.';

    component.executeSearch();

    const chunks = component.retrievedChunks();
    expect(chunks.length).toBe(1);
    expect(chunks[0].content).toContain('HNSW indexes on pgvector');
    expect(chunks[0].cosineSim).toBe(0.915);
  });

  it('should index and chunk document content', () => {
    component.documentContent = 'New enterprise documentation chunk for RAG search testing.';
    component.indexAndChunk();

    const chunks = component.retrievedChunks();
    expect(chunks.length).toBe(1);
    expect(chunks[0].content).toBe('New enterprise documentation chunk for RAG search testing.');
  });

  it('should ignore indexAndChunk when documentContent is empty', () => {
    const initialChunks = component.retrievedChunks();
    component.documentContent = '';
    component.indexAndChunk();

    expect(component.retrievedChunks()).toBe(initialChunks);
    expect(component.retrievedChunks().length).toBe(1);
  });
});
