import { Injectable, signal, computed, effect } from '@angular/core';

export type SupportedLocale = 'en-US' | 'fr-FR' | 'es-ES' | 'de-DE';

export interface LocaleInfo {
  code: SupportedLocale;
  name: string;
  flag: string;
  currency: string;
  dateFormat: string;
}

export const SUPPORTED_LOCALES: LocaleInfo[] = [
  { code: 'en-US', name: 'English (US)', flag: '🇺🇸', currency: 'USD', dateFormat: 'MM/DD/YYYY' },
  { code: 'fr-FR', name: 'Français', flag: '🇫🇷', currency: 'EUR', dateFormat: 'DD/MM/YYYY' },
  { code: 'es-ES', name: 'Español', flag: '🇪🇸', currency: 'EUR', dateFormat: 'DD/MM/YYYY' },
  { code: 'de-DE', name: 'Deutsch', flag: '🇩🇪', currency: 'EUR', dateFormat: 'DD.MM.YYYY' }
];

type TranslationDictionary = Record<string, string>;

const EN_US_TRANSLATIONS: TranslationDictionary = {
  // Brand & Navigation
  'brand.title': 'SGrowing',
  'brand.subtitle': 'AI Omnicommunication Platform',
  'nav.title': 'Navigation',
  'nav.inbox': 'Conversations',
  'nav.all_conversations': 'All Conversations',
  'nav.courier': 'Courier Driver Companion',
  'nav.checkout': '1-Page COD Checkout',
  'nav.creatives': 'AI UGC & Competitor Radar',
  'nav.digital_goods': 'Digital Goods & PIM',
  'nav.forms': 'Dynamic Forms & PDF',
  'nav.developer': 'Developer API & Webhooks',
  'nav.calendar': 'Appointment Calendar',
  'nav.deliverability': 'Email Deliverability',
  'nav.telemetry': 'Live Product Telemetry',
  'nav.billing': 'SaaS Billing & Plans',
  'nav.operations': 'COD Ops & Logistics Hub',
  'nav.analytics': 'Analytics & Multi-Touch BI',
  'nav.commerce': 'Commerce & CPQ',
  'nav.connectors': 'E-Commerce Sync Bridges',
  'nav.funnels': 'AI Funnel & Upsell Studio',
  'nav.seo': 'Technical SEO & SERP Hub',
  'nav.outbound': 'AI SDR Outbound',
  'nav.social': 'Social Media Studio',
  'nav.voice': 'Voice & Softphone',
  'nav.accounts': 'B2B Accounts CRM',
  'nav.workflows': 'Node Workflows',
  'nav.contracts': 'E-Signatures & Deals',
  'nav.rag': 'Vector RAG pgvector',
  'nav.broadcasts': 'WA Broadcasts',
  'nav.sla': 'SLA & Escalations',
  'nav.journeys': 'Customer Journeys',
  'nav.feedback': 'Feedback & CSAT',
  'nav.help': 'Help Center & Bot',
  'nav.chatbots': 'Chatbot Flow DAG & Triggers',
  'nav.warmup': 'P2P Warmup & Spam Rescue',
  'nav.softphone': 'WebRTC Softphone & Fleet',
  'nav.cpq': 'Enterprise CPQ & EAV',
  'nav.sealing': 'Cryptographic PDF Sealing',
  'nav.agents_hub': 'Autonomous AI Agent Hub',
  'nav.missions': 'Agent Missions & SDR Fleet',
  'nav.projects': 'Sprint Tasks & Onboarding',
  'nav.research': 'Deep AI Web Research',
  'nav.warehouses': 'Multi-Warehouse Allocation',
  'nav.organizations': 'Organization & Margin Tracker',
  'nav.timesheets': 'Timesheets & Labor Costing',
  'nav.whatsapp_gateway': 'Native WhatsApp Gateway',
  'nav.autonomous_funnels': 'Autonomous Funnel Workshop',
  'nav.agent_fleet': 'Dual-Layer 38 Agent Fleet',

  // Common Actions & Statuses
  'common.search': 'Search conversations...',
  'common.save': 'Save Changes',
  'common.cancel': 'Cancel',
  'common.delete': 'Delete',
  'common.resolve': 'Resolve',
  'common.active': 'Active',
  'common.inactive': 'Inactive',
  'common.healthy': 'Healthy',
  'common.warning': 'Warning',
  'common.critical': 'Critical',
  'common.loading': 'Loading...',
  'common.refresh': 'Refresh',
  'common.sync_now': 'Sync Now',
  'common.export': 'Export Data',
  'common.seed_sandbox': 'Seed Sandbox',
  'common.exit': 'Exit',
  'common.connected': 'Real-Time Connected',
  'common.mock_mode': 'Local Mock Mode',
  'common.shortcuts_hint': 'Keyboard Shortcuts Active: Ctrl+Shift+Space (Macros) • Ctrl+Alt+D (Dialer) • Ctrl+Alt+M (Missions)',

  // CPQ & Deals
  'cpq.quote_builder': 'Interactive CPQ Proposal Builder',
  'cpq.subtotal': 'Subtotal',
  'cpq.discount': 'Discount',
  'cpq.tax': 'Tax',
  'cpq.total': 'Total',
  'cpq.sign_and_seal': 'Digitally Sign & Seal Proposal',
  'cpq.items': 'Line Items',
  'cpq.unit_price': 'Unit Price',
  'cpq.quantity': 'Quantity',

  // Analytics
  'analytics.live_visitors': 'Live Active Visitors',
  'analytics.conversion_rate': 'Conversion Rate',
  'analytics.revenue_attributed': 'Attributed Revenue',
  'analytics.top_pages': 'Top Performing Pages',

  // E-Commerce Connectors
  'commerce.connectors_title': 'E-Commerce Storefront Sync Bridges',
  'commerce.connectors_subtitle': '2-way synchronization for Shopify and WooCommerce with cryptographic webhook verification',
  'commerce.shopify_config': 'Shopify Store Connection',
  'commerce.woocommerce_config': 'WooCommerce Store Connection',
  'commerce.sync_stats': 'Synchronization Performance',
  'commerce.recent_orders': 'Recent Inbound Orders & Deals',
  'commerce.synced_deals': 'CRM Deals Created',
  'commerce.cod_shipments': 'COD Shipments Dispatched',
  'commerce.trigger_sync': 'Trigger Catalog Sync',

  // Daemons & Watchdog
  'daemons.title': 'Background Daemons & Health Watchdog',
  'daemons.all_healthy': 'All Daemons Operational',
  'daemons.missed_ticks': 'Missed Ticks',
  'daemons.last_heartbeat': 'Last Heartbeat'
};

const FR_FR_TRANSLATIONS: TranslationDictionary = {
  // Brand & Navigation
  'brand.title': 'SGrowing',
  'brand.subtitle': 'Plateforme d’Omnicommunication IA',
  'nav.title': 'Navigation',
  'nav.inbox': 'Conversations',
  'nav.all_conversations': 'Toutes les conversations',
  'nav.courier': 'Compagnon Chauffeur Livreur',
  'nav.checkout': 'Paiement COD en 1 page',
  'nav.creatives': 'IA UGC & Radar Concurrentiel',
  'nav.digital_goods': 'Produits Numériques & PIM',
  'nav.forms': 'Formulaires Dynamiques & PDF',
  'nav.developer': 'API Développeur & Webhooks',
  'nav.calendar': 'Calendrier de Rendez-vous',
  'nav.deliverability': 'Délivrabilité E-mails',
  'nav.telemetry': 'Télémétrie Produit en Direct',
  'nav.billing': 'Facturation SaaS & Forfaits',
  'nav.operations': 'Opérations COD & Hub Logistique',
  'nav.analytics': 'Analytique & BI Multi-Touch',
  'nav.commerce': 'Commerce & CPQ',
  'nav.connectors': 'Ponts de Synchronisation E-Commerce',
  'nav.funnels': 'Tunnels IA & Ventes Croisées',
  'nav.seo': 'SEO Technique & Hub SERP',
  'nav.outbound': 'Prospection IA SDR',
  'nav.social': 'Studio Réseaux Sociaux',
  'nav.voice': 'Voix & Téléphonie Virtuelle',
  'nav.accounts': 'CRM Comptes B2B',
  'nav.workflows': 'Flux de Travail Nodaux',
  'nav.contracts': 'Signatures Électroniques & Contrats',
  'nav.rag': 'RAG Vectoriel pgvector',
  'nav.broadcasts': 'Diffusions WhatsApp',
  'nav.sla': 'SLA & Escalades',
  'nav.journeys': 'Parcours Clients',
  'nav.feedback': 'Avis & CSAT',
  'nav.help': 'Centre d’Aide & Assistant',
  'nav.chatbots': 'DAG de Chatbots & Déclencheurs',
  'nav.warmup': 'Préchauffage P2P & Sauvetage Spam',
  'nav.softphone': 'Softphone WebRTC & Flotte',
  'nav.cpq': 'CPQ Entreprise & EAV',
  'nav.sealing': 'Scellement Cryptographique PDF',
  'nav.agents_hub': 'Centre d’Agents IA Autonomes',
  'nav.missions': 'Missions d’Agents & Flotte SDR',

  // Common Actions & Statuses
  'common.search': 'Rechercher des conversations...',
  'common.save': 'Enregistrer les modifications',
  'common.cancel': 'Annuler',
  'common.delete': 'Supprimer',
  'common.resolve': 'Résoudre',
  'common.active': 'Actif',
  'common.inactive': 'Inactif',
  'common.healthy': 'Opérationnel',
  'common.warning': 'Avertissement',
  'common.critical': 'Critique',
  'common.loading': 'Chargement...',
  'common.refresh': 'Actualiser',
  'common.sync_now': 'Synchroniser maintenant',
  'common.export': 'Exporter les données',
  'common.seed_sandbox': 'Alimenter Bac à Sable',
  'common.exit': 'Quitter',
  'common.connected': 'Connecté en Temps Réel',
  'common.mock_mode': 'Mode Simulation Locale',
  'common.shortcuts_hint': 'Raccourcis Clavier Actifs : Ctrl+Maj+Espace (Macros) • Ctrl+Alt+D (Composeur) • Ctrl+Alt+M (Missions)',

  // CPQ & Deals
  'cpq.quote_builder': 'Générateur Interactif de Devis CPQ',
  'cpq.subtotal': 'Sous-total',
  'cpq.discount': 'Remise',
  'cpq.tax': 'TVA',
  'cpq.total': 'Total',
  'cpq.sign_and_seal': 'Signer et Sceller le Devis',
  'cpq.items': 'Articles',
  'cpq.unit_price': 'Prix unitaire',
  'cpq.quantity': 'Quantité',

  // Analytics
  'analytics.live_visitors': 'Visiteurs Actifs en Direct',
  'analytics.conversion_rate': 'Taux de Conversion',
  'analytics.revenue_attributed': 'Revenu Attribué',
  'analytics.top_pages': 'Pages les Plus Performantes',

  // E-Commerce Connectors
  'commerce.connectors_title': 'Ponts de Synchronisation E-Commerce',
  'commerce.connectors_subtitle': 'Synchronisation bidirectionnelle Shopify et WooCommerce avec validation cryptographique',
  'commerce.shopify_config': 'Configuration Boutique Shopify',
  'commerce.woocommerce_config': 'Configuration Boutique WooCommerce',
  'commerce.sync_stats': 'Performance de Synchronisation',
  'commerce.recent_orders': 'Commandes Récentes & Opportunités',
  'commerce.synced_deals': 'Opportunités CRM Créées',
  'commerce.cod_shipments': 'Expéditions COD Expédiées',
  'commerce.trigger_sync': 'Lancer la Synchronisation du Catalogue',

  // Daemons & Watchdog
  'daemons.title': 'Démons d’Arrière-Plan & Surveillance',
  'daemons.all_healthy': 'Tous les Démons Opérationnels',
  'daemons.missed_ticks': 'Tics Manqués',
  'daemons.last_heartbeat': 'Dernier Battement'
};

const ES_ES_TRANSLATIONS: TranslationDictionary = {
  // Brand & Navigation
  'brand.title': 'SGrowing',
  'brand.subtitle': 'Plataforma de Omnicomunicación IA',
  'nav.title': 'Navegación',
  'nav.inbox': 'Conversaciones',
  'nav.all_conversations': 'Todas las conversaciones',
  'nav.courier': 'Compañero Conductor de Entregas',
  'nav.checkout': 'Pago COD en 1 Página',
  'nav.creatives': 'IA UGC y Radar Competitivo',
  'nav.digital_goods': 'Bienes Digitales y PIM',
  'nav.forms': 'Formularios Dinámicos y PDF',
  'nav.developer': 'API de Desarrollador y Webhooks',
  'nav.calendar': 'Calendario de Citas',
  'nav.deliverability': 'Entregabilidad de Correo',
  'nav.telemetry': 'Telemetría de Producto en Vivo',
  'nav.billing': 'Facturación SaaS y Planes',
  'nav.operations': 'Operaciones COD y Centro Logístico',
  'nav.analytics': 'Analítica y BI Multitoque',
  'nav.commerce': 'Comercio y CPQ',
  'nav.connectors': 'Puentes de Sincronización E-Commerce',
  'nav.funnels': 'Embudos IA y Ventas Adicionales',
  'nav.seo': 'SEO Técnico y Centro SERP',
  'nav.outbound': 'Prospección Saliente IA SDR',
  'nav.social': 'Estudio de Redes Sociales',
  'nav.voice': 'Voz y Telefonía Virtual',
  'nav.accounts': 'CRM Cuentas B2B',
  'nav.workflows': 'Flujos de Trabajo Nodales',
  'nav.contracts': 'Firmas Digitales y Contratos',
  'nav.rag': 'RAG Vectorial pgvector',
  'nav.broadcasts': 'Difusiones de WhatsApp',
  'nav.sla': 'SLA y Escalaciones',
  'nav.journeys': 'Rutas del Cliente',
  'nav.feedback': 'Comentarios y CSAT',
  'nav.help': 'Centro de Ayuda y Asistente',
  'nav.chatbots': 'DAG de Chatbots y Disparadores',
  'nav.warmup': 'Calentamiento P2P y Rescate de Spam',
  'nav.softphone': 'Softphone WebRTC y Flota',
  'nav.cpq': 'CPQ Empresarial y EAV',
  'nav.sealing': 'Sellado Criptográfico PDF',
  'nav.agents_hub': 'Centro de Agentes IA Autónomos',
  'nav.missions': 'Misiones de Agentes y Flota SDR',

  // Common Actions & Statuses
  'common.search': 'Buscar conversaciones...',
  'common.save': 'Guardar Cambios',
  'common.cancel': 'Cancelar',
  'common.delete': 'Eliminar',
  'common.resolve': 'Resolver',
  'common.active': 'Activo',
  'common.inactive': 'Inactivo',
  'common.healthy': 'Operativo',
  'common.warning': 'Advertencia',
  'common.critical': 'Crítico',
  'common.loading': 'Cargando...',
  'common.refresh': 'Actualizar',
  'common.sync_now': 'Sincronizar Ahora',
  'common.export': 'Exportar Datos',
  'common.seed_sandbox': 'Poblar Entorno de Pruebas',
  'common.exit': 'Salir',
  'common.connected': 'Conectado en Tiempo Real',
  'common.mock_mode': 'Modo Simulación Local',
  'common.shortcuts_hint': 'Atajos de Teclado Activos: Ctrl+Shift+Espacio (Macros) • Ctrl+Alt+D (Marcador) • Ctrl+Alt+M (Misiones)',

  // CPQ & Deals
  'cpq.quote_builder': 'Creador Interactivo de Propuestas CPQ',
  'cpq.subtotal': 'Subtotal',
  'cpq.discount': 'Descuento',
  'cpq.tax': 'Impuestos',
  'cpq.total': 'Total',
  'cpq.sign_and_seal': 'Firmar y Sellar Digitalmente',
  'cpq.items': 'Artículos',
  'cpq.unit_price': 'Precio Unitario',
  'cpq.quantity': 'Cantidad',

  // Analytics
  'analytics.live_visitors': 'Visitantes Activos en Vivo',
  'analytics.conversion_rate': 'Tasa de Conversión',
  'analytics.revenue_attributed': 'Ingresos Atribuidos',
  'analytics.top_pages': 'Páginas con Mayor Rendimiento',

  // E-Commerce Connectors
  'commerce.connectors_title': 'Puentes de Sincronización E-Commerce',
  'commerce.connectors_subtitle': 'Sincronización bidireccional para Shopify y WooCommerce con verificación criptográfica',
  'commerce.shopify_config': 'Configuración Tienda Shopify',
  'commerce.woocommerce_config': 'Configuración Tienda WooCommerce',
  'commerce.sync_stats': 'Rendimiento de Sincronización',
  'commerce.recent_orders': 'Pedidos Recientes y Oportunidades CRM',
  'commerce.synced_deals': 'Oportunidades CRM Creadas',
  'commerce.cod_shipments': 'Envíos COD Despachados',
  'commerce.trigger_sync': 'Iniciar Sincronización de Catálogo',

  // Daemons & Watchdog
  'daemons.title': 'Demonios de Fondo y Vigilante de Estado',
  'daemons.all_healthy': 'Todos los Demonios Operativos',
  'daemons.missed_ticks': 'Tics Perdidos',
  'daemons.last_heartbeat': 'Último Latido'
};

const DE_DE_TRANSLATIONS: TranslationDictionary = {
  // Brand & Navigation
  'brand.title': 'SGrowing',
  'brand.subtitle': 'KI-Omnikommunikationsplattform',
  'nav.title': 'Navigation',
  'nav.inbox': 'Konversationen',
  'nav.all_conversations': 'Alle Konversationen',
  'nav.courier': 'Kurierfahrer-Begleiter',
  'nav.checkout': '1-Seiten-COD-Kasse',
  'nav.creatives': 'KI UGC & Wettbewerbs-Radar',
  'nav.digital_goods': 'Digitale Güter & PIM',
  'nav.forms': 'Dynamische Formulare & PDF',
  'nav.developer': 'Entwickler-API & Webhooks',
  'nav.calendar': 'Terminkalender',
  'nav.deliverability': 'E-Mail-Zustellbarkeit',
  'nav.telemetry': 'Live-Produkt-Telemetrie',
  'nav.billing': 'SaaS-Abrechnung & Tarife',
  'nav.operations': 'COD-Betrieb & Logistik-Hub',
  'nav.analytics': 'Analytik & Multi-Touch-BI',
  'nav.commerce': 'Commerce & CPQ',
  'nav.connectors': 'E-Commerce-Synchronisationsbrücken',
  'nav.funnels': 'KI-Trichter & Upselling-Studio',
  'nav.seo': 'Technisches SEO & SERP-Hub',
  'nav.outbound': 'KI-SDR-Outbound',
  'nav.social': 'Social-Media-Studio',
  'nav.voice': 'Sprach- & Telefonie-Studio',
  'nav.accounts': 'B2B-Unternehmens-CRM',
  'nav.workflows': 'Knoten-Workflows',
  'nav.contracts': 'Digitale Signaturen & Verträge',
  'nav.rag': 'Vektor-RAG pgvector',
  'nav.broadcasts': 'WhatsApp-Broadcasts',
  'nav.sla': 'SLA & Eskalationen',
  'nav.journeys': 'Kundenreisen',
  'nav.feedback': 'Kundenfeedback & CSAT',
  'nav.help': 'Hilfezentrum & KI-Bot',
  'nav.chatbots': 'Chatbot-Ablauf-DAG & Trigger',
  'nav.warmup': 'P2P-Aufwärmung & Spam-Rettung',
  'nav.softphone': 'WebRTC-Softphone & Flotte',
  'nav.cpq': 'Enterprise CPQ & EAV',
  'nav.sealing': 'Kryptografische PDF-Siegelung',
  'nav.agents_hub': 'Autonomes KI-Agenten-Hub',
  'nav.missions': 'Agenten-Missionen & SDR-Flotte',

  // Common Actions & Statuses
  'common.search': 'Konversationen durchsuchen...',
  'common.save': 'Änderungen speichern',
  'common.cancel': 'Abbrechen',
  'common.delete': 'Löschen',
  'common.resolve': 'Abschließen',
  'common.active': 'Aktiv',
  'common.inactive': 'Inaktiv',
  'common.healthy': 'Betriebsbereit',
  'common.warning': 'Warnung',
  'common.critical': 'Kritisch',
  'common.loading': 'Wird geladen...',
  'common.refresh': 'Aktualisieren',
  'common.sync_now': 'Jetzt synchronisieren',
  'common.export': 'Daten exportieren',
  'common.seed_sandbox': 'Sandbox befüllen',
  'common.exit': 'Beenden',
  'common.connected': 'In Echtzeit verbunden',
  'common.mock_mode': 'Lokaler Simulationsmodus',
  'common.shortcuts_hint': 'Tastaturkürzel aktiv: Strg+Umschalt+Leertaste (Makros) • Strg+Alt+D (Wählfeld) • Strg+Alt+M (Missionen)',

  // CPQ & Deals
  'cpq.quote_builder': 'Interaktiver CPQ-Angebotsgenerator',
  'cpq.subtotal': 'Zwischensumme',
  'cpq.discount': 'Rabatt',
  'cpq.tax': 'MwSt.',
  'cpq.total': 'Gesamtsumme',
  'cpq.sign_and_seal': 'Angebot digital signieren und versiegeln',
  'cpq.items': 'Positionen',
  'cpq.unit_price': 'Einzelpreis',
  'cpq.quantity': 'Menge',

  // Analytics
  'analytics.live_visitors': 'Aktive Live-Besucher',
  'analytics.conversion_rate': 'Konversionsrate',
  'analytics.revenue_attributed': 'Zugeordneter Umsatz',
  'analytics.top_pages': 'Erfolgreichste Seiten',

  // E-Commerce Connectors
  'commerce.connectors_title': 'E-Commerce-Synchronisationsbrücken',
  'commerce.connectors_subtitle': 'Bidirektionale Synchronisation für Shopify und WooCommerce mit kryptografischer Webhook-Verifikation',
  'commerce.shopify_config': 'Shopify-Store-Verbindung',
  'commerce.woocommerce_config': 'WooCommerce-Store-Verbindung',
  'commerce.sync_stats': 'Synchronisationsleistung',
  'commerce.recent_orders': 'Aktuelle Bestellungen & Deals',
  'commerce.synced_deals': 'Erstellte CRM-Deals',
  'commerce.cod_shipments': 'Versendete COD-Lieferungen',
  'commerce.trigger_sync': 'Katalogsynchronisation starten',

  // Daemons & Watchdog
  'daemons.title': 'Hintergrund-Dienste & Gesundheits-Watchdog',
  'daemons.all_healthy': 'Alle Dienste betriebsbereit',
  'daemons.missed_ticks': 'Verpasste Ticks',
  'daemons.last_heartbeat': 'Letzter Herzschlag'
};

const TRANSLATION_TABLES: Record<SupportedLocale, TranslationDictionary> = {
  'en-US': EN_US_TRANSLATIONS,
  'fr-FR': FR_FR_TRANSLATIONS,
  'es-ES': ES_ES_TRANSLATIONS,
  'de-DE': DE_DE_TRANSLATIONS
};

const STORAGE_KEY = 'sgrowing_locale';

@Injectable({
  providedIn: 'root'
})
export class TranslationService {
  public readonly supportedLocales = SUPPORTED_LOCALES;

  // Primary Native English default
  public readonly currentLocale = signal<SupportedLocale>(this.getInitialLocale());

  public readonly currentLocaleInfo = computed(() => {
    const locale = this.currentLocale();
    return this.supportedLocales.find(l => l.code === locale) ?? this.supportedLocales[0];
  });

  public readonly activeDictionary = computed<TranslationDictionary>(() => {
    return TRANSLATION_TABLES[this.currentLocale()] || EN_US_TRANSLATIONS;
  });

  constructor() {
    // Enforce strict LTR direction across the document
    this.applyDocumentDirectionAndLocale(this.currentLocale());
  }

  public setLocale(locale: SupportedLocale): void {
    if (TRANSLATION_TABLES[locale]) {
      this.currentLocale.set(locale);
      this.applyDocumentDirectionAndLocale(locale);
      this.persistLocale(locale);
    }
  }

  private persistLocale(locale: SupportedLocale): void {
    try {
      if (typeof window !== 'undefined' && window.localStorage) {
        window.localStorage.setItem(STORAGE_KEY, locale);
      }
    } catch (err) {
      console.warn('Could not persist locale to localStorage', err);
    }
  }

  public t(key: string, interpolations?: Record<string, string | number>): string {
    const dict = this.activeDictionary();
    let text = dict[key] || EN_US_TRANSLATIONS[key] || key;

    if (interpolations) {
      for (const [param, val] of Object.entries(interpolations)) {
        text = text.replace(new RegExp(`\\{${param}\\}`, 'g'), String(val));
      }
    }
    return text;
  }

  public formatCurrency(amount: number, currency?: string): string {
    const loc = this.currentLocale();
    const curr = currency || (loc === 'en-US' ? 'USD' : 'EUR');
    const safeAmount = typeof amount === 'number' && !isNaN(amount) ? amount : 0;
    try {
      return new Intl.NumberFormat(loc, {
        style: 'currency',
        currency: curr
      }).format(safeAmount);
    } catch {
      return `${safeAmount.toFixed(2)} ${curr}`;
    }
  }

  public formatDate(date: Date | string | number | null | undefined, options?: Intl.DateTimeFormatOptions): string {
    if (!date) return '-';
    const loc = this.currentLocale();
    const d = typeof date === 'string' || typeof date === 'number' ? new Date(date) : date;
    if (isNaN(d.getTime())) return '-';

    const defaultOptions: Intl.DateTimeFormatOptions = options || {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    };

    try {
      return new Intl.DateTimeFormat(loc, defaultOptions).format(d);
    } catch {
      return d.toLocaleDateString();
    }
  }

  public formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
    const loc = this.currentLocale();
    try {
      return new Intl.NumberFormat(loc, options).format(value);
    } catch {
      return value.toLocaleString();
    }
  }

  private getInitialLocale(): SupportedLocale {
    if (typeof window !== 'undefined' && window.localStorage) {
      const stored = window.localStorage.getItem(STORAGE_KEY) as SupportedLocale | null;
      if (stored && TRANSLATION_TABLES[stored]) {
        return stored;
      }
    }
    return 'en-US'; // Primary native English default
  }

  private applyDocumentDirectionAndLocale(locale: SupportedLocale): void {
    if (typeof document !== 'undefined' && document.documentElement) {
      // STRICT REQUIREMENT: Maintain strict LTR layout direction (dir="ltr") - NOT RTL / Arabic
      document.documentElement.setAttribute('dir', 'ltr');
      document.documentElement.setAttribute('lang', locale);
    }
  }
}
