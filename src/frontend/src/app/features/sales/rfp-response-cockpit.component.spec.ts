// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { RfpResponseCockpitComponent } from './rfp-response-cockpit.component';

describe('RfpResponseCockpitComponent', () => {
  it('should initialize with questionnaires and answers', () => {
    const comp = new RfpResponseCockpitComponent();
    expect(comp.questionnaires().length).toBe(2);
    expect(comp.answers().length).toBe(1);
    expect(comp.questionnaires()[0].confidenceScoreAvgPct).toBeGreaterThan(90);
  });

  it('should generate new answer and prepend to list', () => {
    const comp = new RfpResponseCockpitComponent();
    comp.generateNewAnswer();
    expect(comp.answers().length).toBe(2);
    expect(comp.answers()[0].questionPrompt).toContain('failover');
    expect(comp.answers()[0].synthesizedAnswer).toContain('multi-cloud');
  });

  it('should verify questionnaire completion progress and security compliance references', () => {
    const comp = new RfpResponseCockpitComponent();
    const soc2Rfp = comp.questionnaires().find(q => q.questionnaireType === 'SECURITY_SOC2_ISO');
    expect(soc2Rfp).toBeDefined();
    expect(soc2Rfp?.answeredQuestionsCount).toBe(115);
    expect(soc2Rfp?.totalQuestionsCount).toBe(120);

    const initialAnswer = comp.answers()[0];
    expect(initialAnswer.canonicalSourceReference).toContain('ISO/IEC 27001');
    expect(initialAnswer.synthesizedAnswer).toContain('AES-256-GCM');
  });
});
