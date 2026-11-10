import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, input } from '@angular/core';
import {
  Archive,
  ArchiveRestore,
  ArrowDown,
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  ArrowUpDown,
  Banknote,
  BriefcaseBusiness,
  Building2,
  Calendar,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  Clock,
  Columns3,
  CornerDownLeft,
  Download,
  Ellipsis,
  ExternalLink,
  Globe,
  GripVertical,
  History,
  type IconNode,
  Inbox,
  Info,
  Keyboard,
  LayoutGrid,
  ListFilter,
  LoaderCircle,
  LogOut,
  MapPin,
  Monitor,
  Moon,
  MoveRight,
  Pencil,
  Plus,
  Rows3,
  Search,
  Settings,
  SquareKanban,
  Star,
  Sun,
  Trash2,
  TriangleAlert,
  Upload,
  X,
} from 'lucide';

/** The only icon set in the app (Lucide). Add an icon here before using its name. */
const ICONS = {
  archive: Archive,
  'archive-restore': ArchiveRestore,
  'arrow-down': ArrowDown,
  'arrow-left': ArrowLeft,
  'arrow-right': ArrowRight,
  'arrow-up': ArrowUp,
  'arrow-up-down': ArrowUpDown,
  banknote: Banknote,
  briefcase: BriefcaseBusiness,
  building: Building2,
  calendar: Calendar,
  check: Check,
  'chevron-down': ChevronDown,
  'chevron-left': ChevronLeft,
  'chevron-right': ChevronRight,
  'circle-alert': CircleAlert,
  clock: Clock,
  columns: Columns3,
  enter: CornerDownLeft,
  download: Download,
  ellipsis: Ellipsis,
  external: ExternalLink,
  globe: Globe,
  grip: GripVertical,
  history: History,
  inbox: Inbox,
  info: Info,
  keyboard: Keyboard,
  dashboard: LayoutGrid,
  filter: ListFilter,
  loader: LoaderCircle,
  logout: LogOut,
  'map-pin': MapPin,
  monitor: Monitor,
  moon: Moon,
  move: MoveRight,
  pencil: Pencil,
  plus: Plus,
  rows: Rows3,
  search: Search,
  settings: Settings,
  kanban: SquareKanban,
  star: Star,
  sun: Sun,
  trash: Trash2,
  warning: TriangleAlert,
  upload: Upload,
  x: X,
} satisfies Record<string, IconNode>;

export type IconName = keyof typeof ICONS;

const SVG_NS = 'http://www.w3.org/2000/svg';

@Component({
  selector: 'app-icon',
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true', '[class.spin]': "name() === 'loader'" },
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      line-height: 0;
    }
    :host(.spin) {
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
  readonly size = input(16);
  readonly strokeWidth = input(1.75);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  constructor() {
    effect(() => {
      const svg = document.createElementNS(SVG_NS, 'svg');
      const size = String(this.size());
      for (const [key, value] of Object.entries({
        width: size,
        height: size,
        viewBox: '0 0 24 24',
        fill: 'none',
        stroke: 'currentColor',
        'stroke-width': String(this.strokeWidth()),
        'stroke-linecap': 'round',
        'stroke-linejoin': 'round',
        focusable: 'false',
      })) {
        svg.setAttribute(key, value);
      }
      for (const [tag, attributes] of ICONS[this.name()]) {
        const child = document.createElementNS(SVG_NS, tag);
        for (const [key, value] of Object.entries(attributes)) {
          child.setAttribute(key, String(value));
        }
        svg.appendChild(child);
      }
      this.host.replaceChildren(svg);
    });
  }
}
