export const familyOverview = {
  population: 120,
  adults: 90,
  minors: 30,
  pairedGenoses: 60,
  singleAdults: 30,
  partnerships: 30,
  parentEdges: 48,
  guardianEdges: 2,
  exPartnerships: 4,
  seedPartnershipShare: 0.8,
  partnershipRate: 0.5,
  sameSexPairingAllowed: false,
  cohabitOnPairing: false,
  birthsThisYear: 7,
  birthRate: 58.3,
  naturalChange: 5,
  orphans: 3,
  stateCareChildren: 2,
  minorTrustTotal: 15000,
  inheritancePaidThisYear: 42000,
  trustReleasedThisYear: 8000,
  replacementRate: 1.05,
  traits: {
    population: 120,
    withTraits: 118,
    missingTraits: 2,
    migrated: 0,
    mutationEvents: 4,
    femaleShare: 0.51,
    categories: [
      {
        name: "Hair",
        values: [
          { value: "Brown", count: 70, share: 0.59 },
          { value: "Blonde", count: 30, share: 0.25 }
        ]
      },
      {
        name: "Temperament",
        values: [{ value: "Analytical", count: 45, share: 0.38 }]
      }
    ]
  },
  households: [
    {
      networkId: 500,
      headNetworkId: 101,
      head: "Ada Lovelace",
      members: 4,
      children: 2,
      dependants: 2,
      earners: 2,
      capacity: 5,
      occupantCount: 4,
      overcrowded: false,
      unitNetworkId: 900,
      unit: "Willow House",
      tenure: "Owned"
    },
    {
      networkId: 501,
      headNetworkId: 201,
      head: "Grace Hopper",
      members: 6,
      children: 3,
      dependants: 3,
      earners: 2,
      capacity: 4,
      occupantCount: 6,
      overcrowded: true,
      unitNetworkId: 901,
      unit: "Harbour Flat",
      tenure: "Rented"
    }
  ],
  projection: {
    source: "live",
    liveTick: 4200,
    projectedTick: 4200,
    isStale: false,
    detail: "Family read model reads live simulation state."
  },
  performance: {
    lastTickMilliseconds: 1.8,
    p95TickMilliseconds: 3.1,
    p99TickMilliseconds: 7.4,
    allocatedBytesPerTick: 20480,
    candidateGenoses: 120,
    candidateAverage: 3,
    candidateP95: 5,
    candidateMax: 5,
    candidateCap: 24,
    worstSystem: "Attraction",
    worstSystemAverageMilliseconds: 1.2
  },
  health: [
    {
      name: "Relationship symmetry",
      level: "Pass",
      detail: "All mirror edges agree.",
      entityNetworkId: null,
      entityKind: null,
      link: null
    },
    {
      name: "Trust conservation",
      level: "Warn",
      detail: "One trust is older than the review window.",
      entityNetworkId: 501,
      entityKind: "Trust",
      link: "/family"
    }
  ]
};

export const familyGenos = {
  networkId: 101,
  name: "Ada Lovelace",
  age: 34,
  stage: "Work",
  sex: "Female",
  householdNetworkId: 500,
  household: "Willow House",
  partnerNetworkId: 102,
  partnerName: "Charles Babbage",
  guardianNetworkId: null,
  isPaired: true,
  hasChildren: true,
  isOrphan: false,
  inStateCare: false,
  parentCount: 0,
  childCount: 2,
  traits: [{ value: "Analytical", count: 1, share: 1 }]
};

export const familyGenosesPage = {
  total: 1,
  skip: 0,
  take: 200,
  genoses: [familyGenos]
};

export const familyGenosDetail = {
  genos: familyGenos,
  relationships: [
    {
      networkId: 1,
      fromGenosId: 101,
      fromName: "Ada Lovelace",
      toGenosId: 102,
      toName: "Charles Babbage",
      kind: "Partner",
      source: "Seed",
      sinceTick: 10
    }
  ],
  pregnancies: [],
  children: [
    {
      networkId: 301,
      name: "Byron",
      age: 6,
      parentANetworkId: 101,
      parentBNetworkId: 102,
      guardianNetworkId: null,
      householdNetworkId: 500,
      household: "Willow House",
      enrolled: true,
      unenrolled: false,
      school: "Analytical Engine School",
      overcrowded: false,
      inStateCare: false,
      childCostsThisYear: 120
    }
  ],
  guardianships: [],
  household: {
    networkId: 500,
    headNetworkId: 101,
    head: "Ada Lovelace",
    members: 4,
    children: 2,
    dependants: 2,
    earners: 2,
    capacity: 5,
    occupantCount: 4,
    overcrowded: false,
    unitNetworkId: 900,
    unit: "Willow House",
    tenure: "Owned"
  }
};

export const familyRelationshipsPage = {
  total: 1,
  skip: 0,
  take: 200,
  relationships: [
    {
      networkId: 1,
      fromGenosId: 101,
      fromName: "Ada Lovelace",
      toGenosId: 102,
      toName: "Charles Babbage",
      kind: "Partner",
      source: "Seed",
      sinceTick: 10
    }
  ]
};

export const familyAttraction = {
  genosId: 101,
  candidateId: 102,
  eligible: true,
  reasonCode: null,
  reason: null,
  score: 42,
  components: [
    { name: "Trait match", points: 20, max: 25 },
    { name: "Age proximity", points: 22, max: 25 }
  ]
};

export const familyPregnanciesPage = {
  total: 1,
  skip: 0,
  take: 200,
  activeOnly: false,
  pregnancies: [
    {
      networkId: 700,
      carryingParentNetworkId: 102,
      otherParentNetworkId: 101,
      carryingParentName: "Charles Babbage",
      otherParentName: "Ada Lovelace",
      conceivedTick: 4000,
      dueTick: 4300,
      monthsRemaining: 3,
      riskBasisPoints: 250,
      status: "Active",
      source: "Attraction",
      childrenExpected: 1,
      complications: 0,
      careMonths: 4,
      missedCareMonths: 0,
      carePaid: 1200,
      careArrears: 0,
      resolvedTick: 0
    }
  ]
};

export const familyChildrenPage = {
  total: 1,
  skip: 0,
  take: 200,
  ageBand: null,
  children: [
    {
      networkId: 301,
      name: "Byron",
      age: 6,
      parentANetworkId: 101,
      parentBNetworkId: 102,
      guardianNetworkId: null,
      householdNetworkId: 500,
      household: "Willow House",
      enrolled: true,
      unenrolled: false,
      school: "Analytical Engine School",
      overcrowded: false,
      inStateCare: false,
      childCostsThisYear: 120
    }
  ]
};

export const familyGuardianships = [
  {
    guardianNetworkId: 101,
    guardianName: "Ada Lovelace",
    childNetworkId: 302,
    childName: "Lovelace Ward",
    source: "Guardianship",
    sinceTick: 300,
    stateCare: false
  }
];

export const emptyFamilyGenosesPage = { total: 0, skip: 0, take: 200, genoses: [] };
export const emptyFamilyChildrenPage = {
  total: 0,
  skip: 0,
  take: 200,
  ageBand: null,
  children: []
};
export const emptyFamilyGuardianships: unknown[] = [];
