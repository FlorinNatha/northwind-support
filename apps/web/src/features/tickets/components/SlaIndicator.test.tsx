import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

describe('SlaIndicator', () => {
  const cases: { status: SlaStatus; expectedText: string; expectedTone: string }[] = [
    { status: 'WithinSla', expectedText: 'Within SLA', expectedTone: 'info' },
    { status: 'AtRisk', expectedText: 'At risk', expectedTone: 'warning' },
    { status: 'Breached', expectedText: 'Breached', expectedTone: 'danger' },
    { status: 'Met', expectedText: 'Met', expectedTone: 'success' },
    { status: 'NotApplicable', expectedText: 'No SLA', expectedTone: 'neutral' },
  ];

  it.each(cases)('renders $status with label "$expectedText" and tone "$expectedTone"', ({ status, expectedText, expectedTone }) => {
    const { container } = render(<SlaIndicator status={status} />);

    expect(screen.getByText(expectedText)).toBeInTheDocument();
    expect(screen.getByLabelText(`SLA status: ${expectedText}`)).toBeInTheDocument();
    expect(container.querySelector(`.badge--${expectedTone}`)).toBeInTheDocument();
  });

  it('can hide the icon when showIcon is false', () => {
    const { container } = render(<SlaIndicator status="AtRisk" showIcon={false} />);
    expect(screen.getByText('At risk')).toBeInTheDocument();
    expect(container.querySelector('.sla-indicator__icon')).not.toBeInTheDocument();
  });
});
