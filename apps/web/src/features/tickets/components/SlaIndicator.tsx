import { Badge, type BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

interface SlaConfig {
  label: string;
  tone: BadgeTone;
  icon: string;
  description: string;
}

const SLA_CONFIGS: Record<SlaStatus, SlaConfig> = {
  WithinSla: {
    label: 'Within SLA',
    tone: 'info',
    icon: '●',
    description: 'Ticket is open and comfortably within the SLA target time.',
  },
  AtRisk: {
    label: 'At risk',
    tone: 'warning',
    icon: '▲',
    description: 'Ticket is open with 25% or less of the SLA window remaining.',
  },
  Breached: {
    label: 'Breached',
    tone: 'danger',
    icon: '■',
    description: 'SLA target was missed or the due date has already passed.',
  },
  Met: {
    label: 'Met',
    tone: 'success',
    icon: '✓',
    description: 'Ticket was resolved or closed on or before the SLA due date.',
  },
  NotApplicable: {
    label: 'No SLA',
    tone: 'neutral',
    icon: '—',
    description: 'No SLA applies to this ticket.',
  },
};

interface SlaIndicatorProps {
  status: SlaStatus;
  showIcon?: boolean;
  className?: string;
}

/**
 * Reusable SLA indicator component providing distinct color, icon, and text cues
 * for accessible status identification (BR-8).
 */
export function SlaIndicator({ status, showIcon = true, className = '' }: SlaIndicatorProps) {
  const config = SLA_CONFIGS[status] ?? SLA_CONFIGS.NotApplicable;

  return (
    <Badge tone={config.tone}>
      <span
        className={`sla-indicator ${className}`.trim()}
        title={config.description}
        aria-label={`SLA status: ${config.label}`}
      >
        {showIcon && (
          <span className="sla-indicator__icon" aria-hidden="true">
            {config.icon}
          </span>
        )}
        <span className="sla-indicator__label">{config.label}</span>
      </span>
    </Badge>
  );
}
