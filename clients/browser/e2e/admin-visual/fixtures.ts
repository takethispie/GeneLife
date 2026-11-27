/**
 * Deterministic API fixtures for the admin visual-regression baseline.
 *
 * Shapes mirror the C# DTOs under `Genelife.Contracts/Dtos` (and the Family DTOs
 * re-used from `../admin/family-fixtures`). They exist so the admin pages render
 * a populated layout without a live API, a Postgres database or a seeded world —
 * a screenshot baseline must be byte-identical every run, and live data is not.
 *
 * Only fields the pages actually read need to be present, but every list is kept
 * non-empty so the baselines exercise real tables/cards rather than empty states.
 * Timestamps are fixed strings, never `new Date()`, or the baseline would drift.
 */

export {
  familyOverview,
  familyGenosesPage,
  familyGenosDetail,
  familyRelationshipsPage,
  familyAttraction,
  familyPregnanciesPage,
  familyChildrenPage,
  familyGuardianships
} from "../admin/family-fixtures";

const UUID = {
  a: "11111111-1111-1111-1111-111111111111",
  b: "22222222-2222-2222-2222-222222222222",
  c: "33333333-3333-3333-3333-333333333333"
};

const appearanceDto = {
  skinTone: 3,
  faceShape: 2,
  hairColor: 5,
  hairStyle: 1,
  eyeColor: 4,
  bodyFrame: 2,
  heightScale: 5,
  ageBand: 3,
  expression: 1,
  appearanceVersion: 1
};

const appearanceLayers = {
  bodyPart: "body-base",
  skinPalette: "warm-2",
  facePart: "face-oval",
  eyePart: "eyes-round",
  hairPart: "hair-short",
  outfitPart: "outfit-casual",
  expressionPart: "expression-neutral",
  heightScale: 1,
  layerOrderVersion: 1
};

// --- Home / dashboard has no API dependency ---------------------------------

export const simulationStatus = { isRunning: true };

// --- Data (/api/data) -------------------------------------------------------

export const worldData = {
  tick: 12840,
  year: 12,
  month: 4,
  isRunning: true,
  capturedAt: "2026-01-15T09:30:00+00:00",
  genoses: [
    {
      networkId: 101,
      name: "Marceline Okonkwo",
      age: 34,
      isPlayerControlled: true,
      hunger: 0.2,
      energy: 0.7,
      hygiene: 0.9,
      bladder: 0.3,
      currentAction: "Working",
      timeRemaining: 12,
      x: 10.5,
      y: 4.25,
      tileX: 10,
      tileY: 4,
      hasMoveTarget: false,
      moveTargetX: 10,
      moveTargetY: 4,
      walletBalance: 240.5,
      savingsBalance: 1820,
      unmetDemand: 0,
      householdNetworkId: 501,
      housingUnitNetworkId: 601,
      tenure: "Renter",
      housingTier: "Mid",
      homeless: false,
      stage: "Work",
      health: 0.95,
      topSkill: "Engineering",
      seniority: "Mid",
      isDead: false,
      appearance: appearanceDto
    },
    {
      networkId: 102,
      name: "Idris Bakker",
      age: 41,
      isPlayerControlled: false,
      hunger: 0.4,
      energy: 0.5,
      hygiene: 0.8,
      bladder: 0.5,
      currentAction: "Sleeping",
      timeRemaining: 40,
      x: 6.1,
      y: 8.9,
      tileX: 6,
      tileY: 8,
      hasMoveTarget: true,
      moveTargetX: 6,
      moveTargetY: 9,
      walletBalance: 90,
      savingsBalance: 400,
      unmetDemand: 0.1,
      householdNetworkId: 502,
      housingUnitNetworkId: 602,
      tenure: "Owner",
      housingTier: "High",
      homeless: false,
      stage: "Work",
      health: 0.88,
      topSkill: "Medicine",
      seniority: "Senior",
      isDead: false,
      appearance: { ...appearanceDto, hairColor: 2, eyeColor: 1 }
    }
  ],
  companies: [
    {
      networkId: 201,
      name: "Aurora Works",
      employeeCount: 12,
      officeCount: 2,
      balance: 15400.75,
      reservedForPayroll: 2300,
      arrears: 0,
      revenueLastCycle: 4200,
      wagesLastCycle: 3600,
      solvency: "Healthy"
    },
    {
      networkId: 202,
      name: "Bramble Logistics",
      employeeCount: 7,
      officeCount: 1,
      balance: 3200,
      reservedForPayroll: 900,
      arrears: 120,
      revenueLastCycle: 1800,
      wagesLastCycle: 2100,
      solvency: "Strained"
    }
  ],
  offices: [
    { networkId: 301, companyNetworkId: 201, name: "Aurora HQ", district: "Northgate" }
  ],
  employees: [
    {
      networkId: 401,
      genosNetworkId: 101,
      companyNetworkId: 201,
      position: "Engineer",
      seniority: "Mid",
      salary: 210
    }
  ],
  jobPostings: [
    { networkId: 501, companyNetworkId: 201, position: "Engineer", salary: 210, isOpen: true }
  ],
  jobApplications: [],
  openPositions: [
    { postingNetworkId: 501, companyNetworkId: 201, position: "Engineer", salary: 210 }
  ],
  economy: {
    householdCash: 18400,
    householdSavings: 92300,
    companyCash: 18600,
    companyDebt: 4200,
    pensionFund: 58000,
    governmentFund: 12400,
    bankFunds: 31000,
    totalMoneySupply: 198700,
    arrears: 320,
    wagesPaidThisYear: 41200,
    revenueThisYear: 52300,
    taxThisYear: 8600,
    basicIncomeThisYear: 2100,
    unemploymentRate: 0.04,
    vacancyRate: 0.06,
    wageShare: 0.58,
    year: 12,
    month: 4,
    health: [
      { name: "money-conservation", level: "Ok", detail: "Money supply conserved this cycle." },
      { name: "arrears", level: "Warn", detail: "Two households in arrears." }
    ]
  },
  housing: {
    properties: 4,
    units: 16,
    occupiedUnits: 13,
    vacantUnits: 3,
    shelterBeds: 8,
    shelteredHouseholds: 1,
    homelessHouseholds: 0,
    overcrowdedUnits: 1,
    unitsBelowCondition: 2,
    activeProjects: 1,
    constructionUnits: 6,
    vacancyRate: 0.19,
    rentIndex: 1.02,
    rentRoll: 4120,
    rentCollectedThisMonth: 3980,
    utilityCollectedThisMonth: 640,
    upkeepPaidThisMonth: 310,
    rentArrears: 140,
    utilityArrears: 30,
    medianRentBurden: 0.24,
    shelterOccupancy: 0.12,
    evictionsThisYear: 1,
    movesThisYear: 7,
    year: 12,
    month: 4,
    health: [{ name: "vacancy", level: "Ok", detail: "Vacancy within target band." }]
  },
  lifecycle: {
    totalGenoses: 42,
    education: 9,
    work: 24,
    retirement: 6,
    elderly: 3,
    averageAge: 33.4,
    medianAge: 32,
    averageHealth: 0.91,
    workingAgeShare: 0.71,
    oldAgeDependencyRatio: 0.21,
    studentsEnrolled: 8,
    unenrolledStudents: 1,
    enrollmentRate: 0.89,
    graduatesThisYear: 4,
    employed: 23,
    unemployed: 1,
    unemploymentRate: 0.04,
    retiredThisYear: 2,
    deathsThisYear: 1,
    arrivalsThisYear: 5,
    lifeExpectancy: 78.5,
    mortalityRecords: 12,
    pensionFund: 58000,
    pensionLiabilities: 64000,
    pensionFundingRatio: 0.91,
    averagePension: 180,
    pensionReplacementRate: 0.42,
    schools: 2,
    policy: { schoolStartAge: 6, workStartAge: 18, retirementAge: 65, elderlyAge: 70, maxAge: 90 },
    immigration: {
      targetPopulation: 50,
      enabled: true,
      arrivalsThisYear: 5,
      arrivalBudget: 10,
      pending: 1,
      cancelledThisYear: 0,
      paused: false
    },
    latestSnapshot: null,
    health: [{ name: "dependency", level: "Ok", detail: "Dependency ratio within band." }]
  },
  map: {
    mapId: "world-v5",
    version: 3,
    buildings: 4,
    occupiedTiles: 130,
    unplacedBuildings: 0,
    healthIssues: 0
  },
  recentLedger: [
    {
      transactionId: UUID.a,
      tick: 12839,
      kind: "Wage",
      fromNetworkId: 201,
      toNetworkId: 101,
      amount: 210,
      reference: "wage-12839",
      memo: "Monthly wage"
    },
    {
      transactionId: UUID.b,
      tick: 12838,
      kind: "Rent",
      fromNetworkId: 501,
      toNetworkId: 701,
      amount: 120,
      reference: "rent-12838",
      memo: null
    }
  ]
};

// --- Map (/api/map) ---------------------------------------------------------

export const mapStatus = {
  hasMap: true,
  mapId: "world-v5",
  version: 3,
  layoutVersion: 5,
  seed: 11,
  width: 192,
  height: 192,
  tileSize: 1,
  minX: 0,
  minY: 0,
  maxX: 191,
  maxY: 191,
  buildings: 4,
  occupiedTiles: 130,
  reservedTiles: 8,
  unplacedBuildings: 0,
  healthIssues: 0,
  buildingList: [
    {
      networkId: 801,
      kind: "Residential",
      name: "Maple Court",
      district: "Northgate",
      x: 20,
      y: 24,
      width: 6,
      height: 5,
      entryX: 22,
      entryY: 29,
      ownerNetworkId: 701
    },
    {
      networkId: 802,
      kind: "Shop",
      name: "Corner Grocer",
      district: "Market",
      x: 40,
      y: 12,
      width: 4,
      height: 3,
      entryX: 41,
      entryY: 15,
      ownerNetworkId: 202
    },
    {
      networkId: 803,
      kind: "Office",
      name: "Aurora HQ",
      district: "Northgate",
      x: 60,
      y: 40,
      width: 7,
      height: 6,
      entryX: 63,
      entryY: 46,
      ownerNetworkId: 201
    },
    {
      networkId: 804,
      kind: "School",
      name: "Northgate Primary",
      district: "Northgate",
      x: 12,
      y: 55,
      width: 8,
      height: 5,
      entryX: 15,
      entryY: 60,
      ownerNetworkId: null
    }
  ],
  unplaced: []
};

// --- Economy ----------------------------------------------------------------

export const economyOverview = worldData.economy;

export const economyFiscal = {
  governmentBalance: 12400,
  incomeTaxCollected: 6200,
  corporateTaxCollected: 2400,
  benefitsPaid: 2100,
  officeCostsCollected: 640,
  pensionBalance: 58000,
  pensionContributions: 3600,
  bankBalance: 31000,
  bankInterestPool: 420,
  totalLoanOutstanding: 4200
};

export const economyHistory = [
  {
    tick: 11200,
    year: 11,
    householdCash: 16100,
    householdSavings: 88100,
    companyCash: 17200,
    companyDebt: 3900,
    pensionFund: 54200,
    governmentFund: 11000,
    totalMoneySupply: 190200,
    wagesPaid: 38600,
    revenue: 49200,
    tax: 8100,
    arrears: 280,
    unemploymentRate: 0.05,
    vacancyRate: 0.07,
    wageShare: 0.57,
    gini: 0.34,
    bankruptCompanies: 0
  },
  {
    tick: 12840,
    year: 12,
    householdCash: 18400,
    householdSavings: 92300,
    companyCash: 18600,
    companyDebt: 4200,
    pensionFund: 58000,
    governmentFund: 12400,
    totalMoneySupply: 198700,
    wagesPaid: 41200,
    revenue: 52300,
    tax: 8600,
    arrears: 320,
    unemploymentRate: 0.04,
    vacancyRate: 0.06,
    wageShare: 0.58,
    gini: 0.35,
    bankruptCompanies: 0
  }
];

export const economyLedger = worldData.recentLedger;

// --- Housing ----------------------------------------------------------------

export const housingOverview = worldData.housing;

export const housingProperties = [
  {
    networkId: 801,
    name: "Maple Court",
    district: "Northgate",
    ownerNetworkId: 701,
    owner: "Northgate Holdings",
    ownerType: "Company",
    unitCount: 6,
    occupiedUnits: 5,
    vacantUnits: 1,
    condition: 0.82,
    neighbourhood: 0.74,
    monthlyUpkeep: 90,
    isShelter: false,
    units: [
      {
        networkId: 601,
        unitNumber: "1A",
        tier: "Mid",
        capacity: 4,
        occupants: 3,
        baseRent: 120,
        marketRent: 128,
        purchasePrice: 14000,
        condition: 0.8,
        isVacant: false,
        isOvercrowded: false,
        isCondemned: false,
        householdNetworkId: 501,
        household: "Okonkwo"
      },
      {
        networkId: 602,
        unitNumber: "1B",
        tier: "Low",
        capacity: 2,
        occupants: 2,
        baseRent: 80,
        marketRent: 76,
        purchasePrice: 9000,
        condition: 0.55,
        isVacant: false,
        isOvercrowded: true,
        isCondemned: false,
        householdNetworkId: 502,
        household: "Bakker"
      }
    ]
  },
  {
    networkId: 802,
    name: "Ivy Quay",
    district: "Market",
    ownerNetworkId: null,
    owner: "City Housing",
    ownerType: "Government",
    unitCount: 10,
    occupiedUnits: 8,
    vacantUnits: 2,
    condition: 0.7,
    neighbourhood: 0.6,
    monthlyUpkeep: 140,
    isShelter: false,
    units: []
  }
];

export const housingHouseholds = [
  {
    networkId: 501,
    headNetworkId: 101,
    head: "Marceline Okonkwo",
    unitNetworkId: 601,
    unit: "1A",
    district: "Northgate",
    property: "Maple Court",
    tenure: "Renter",
    members: 3,
    earners: 2,
    monthlyRent: 120,
    monthlyIncome: 420,
    rentArrears: 0,
    utilityArrears: 0,
    debt: 200,
    arrearsCycles: 0,
    noticeStage: 0,
    cureDeadlineTick: 0,
    overcrowded: false,
    noUtilities: false,
    homeless: false,
    membersList: ["Marceline Okonkwo", "Idris Bakker", "child"]
  },
  {
    networkId: 502,
    headNetworkId: 102,
    head: "Idris Bakker",
    unitNetworkId: 602,
    unit: "1B",
    district: "Northgate",
    property: "Maple Court",
    tenure: "Renter",
    members: 2,
    earners: 1,
    monthlyRent: 80,
    monthlyIncome: 150,
    rentArrears: 140,
    utilityArrears: 30,
    debt: 0,
    arrearsCycles: 2,
    noticeStage: 1,
    cureDeadlineTick: 13000,
    overcrowded: true,
    noUtilities: false,
    homeless: false,
    membersList: ["Idris Bakker", "child"]
  }
];

export const housingRentRoll = [
  {
    ownerNetworkId: 701,
    owner: "Northgate Holdings",
    ownerType: "Company",
    units: 6,
    occupiedUnits: 5,
    rentRoll: 640,
    rentArrears: 140,
    utilityArrears: 30
  },
  {
    ownerNetworkId: null,
    owner: "City Housing",
    ownerType: "Government",
    units: 10,
    occupiedUnits: 8,
    rentRoll: 520,
    rentArrears: 0,
    utilityArrears: 0
  }
];

export const housingConstruction = [
  {
    networkId: 901,
    developerNetworkId: 201,
    developer: "Aurora Works",
    district: "Market",
    tier: "Mid",
    units: 6,
    monthsRemaining: 4,
    cost: 48000,
    paidToDate: 16000
  }
];

export const housingHistory = [
  {
    tick: 11200,
    year: 11,
    properties: 3,
    units: 12,
    occupiedUnits: 10,
    vacantUnits: 2,
    shelterBeds: 8,
    shelteredHouseholds: 1,
    homelessHouseholds: 0,
    overcrowdedUnits: 1,
    unitsBelowCondition: 2,
    vacancyRate: 0.17,
    rentIndex: 1,
    rentRoll: 3800,
    rentCollected: 3700,
    rentArrears: 120,
    utilityArrears: 20,
    upkeepPaid: 280,
    evictionsThisYear: 0,
    constructionUnits: 0,
    medianRentBurden: 0.23,
    shelterOccupancy: 0.12
  },
  {
    tick: 12840,
    year: 12,
    properties: 4,
    units: 16,
    occupiedUnits: 13,
    vacantUnits: 3,
    shelterBeds: 8,
    shelteredHouseholds: 1,
    homelessHouseholds: 0,
    overcrowdedUnits: 1,
    unitsBelowCondition: 2,
    vacancyRate: 0.19,
    rentIndex: 1.02,
    rentRoll: 4120,
    rentCollected: 3980,
    rentArrears: 140,
    utilityArrears: 30,
    upkeepPaid: 310,
    evictionsThisYear: 1,
    constructionUnits: 6,
    medianRentBurden: 0.24,
    shelterOccupancy: 0.12
  }
];

// --- Lifecycle --------------------------------------------------------------

export const lifecycleOverview = {
  status: worldData.lifecycle,
  schools: [
    {
      networkId: 804,
      name: "Northgate Primary",
      district: "Northgate",
      capacity: 60,
      enrolled: 34,
      attendance: 0.94,
      quality: 0.78,
      emphasis: "General",
      monthlyCost: 1200,
      ownerNetworkId: null,
      owner: "City"
    },
    {
      networkId: 805,
      name: "Market Academy",
      district: "Market",
      capacity: 40,
      enrolled: 18,
      attendance: 0.9,
      quality: 0.83,
      emphasis: "STEM",
      monthlyCost: 1500,
      ownerNetworkId: 201,
      owner: "Aurora Works"
    }
  ],
  pensions: {
    fundBalance: 58000,
    projectedLiabilities: 64000,
    fundingRatio: 0.91,
    members: 6,
    totalContributions: 3600,
    averageMonthlyPension: 180,
    replacementRate: 0.42,
    paidThisCycle: 1080,
    shortfallThisCycle: 0,
    reducedThisCycle: 0,
    memberDetail: [
      {
        networkId: 103,
        name: "Petra Lindqvist",
        age: 68,
        monthlyPension: 190,
        contributions: 12000,
        contributingCycles: 480,
        meansTested: false,
        reduced: false
      }
    ]
  },
  mortality: [
    {
      tick: 12010,
      genosNetworkId: 199,
      name: "Old Willem",
      age: 84,
      cause: "OldAge",
      wealth: 320
    }
  ],
  populationHistory: [
    {
      tick: 11200,
      year: 11,
      total: 38,
      education: 8,
      work: 22,
      retirement: 5,
      elderly: 3,
      studentsEnrolled: 7,
      graduatesThisYear: 3,
      employed: 21,
      unemployed: 1,
      retiredThisYear: 1,
      deathsThisYear: 2,
      arrivalsThisYear: 4,
      averageAge: 32.8,
      medianAge: 31,
      averageHealth: 0.9,
      averageAgeAtDeath: 76,
      lifeExpectancy: 77.8,
      pensionReplacementRate: 0.41,
      pensionFundingRatio: 0.9,
      workingAgeShare: 0.7,
      oldAgeDependencyRatio: 0.22,
      pyramidBuckets: [4, 8, 10, 8, 5, 3],
      deathCauses: [
        { cause: "OldAge", count: 1 },
        { cause: "Illness", count: 1 }
      ]
    },
    {
      tick: 12840,
      year: 12,
      total: 42,
      education: 9,
      work: 24,
      retirement: 6,
      elderly: 3,
      studentsEnrolled: 8,
      graduatesThisYear: 4,
      employed: 23,
      unemployed: 1,
      retiredThisYear: 2,
      deathsThisYear: 1,
      arrivalsThisYear: 5,
      averageAge: 33.4,
      medianAge: 32,
      averageHealth: 0.91,
      averageAgeAtDeath: 78,
      lifeExpectancy: 78.5,
      pensionReplacementRate: 0.42,
      pensionFundingRatio: 0.91,
      workingAgeShare: 0.71,
      oldAgeDependencyRatio: 0.21,
      pyramidBuckets: [5, 9, 11, 9, 5, 3],
      deathCauses: [{ cause: "OldAge", count: 1 }]
    }
  ]
};

export const lifecycleGenoses = [
  {
    networkId: 101,
    name: "Marceline Okonkwo",
    age: 34,
    stage: "Work",
    ageTicks: 408,
    stageEnteredTick: 10000,
    lastTransitionCause: "Graduated",
    health: 0.95,
    skillTotal: 42,
    topSkill: "Engineering",
    isEnrolled: false,
    isUnenrolled: false,
    schoolNetworkId: null,
    school: "",
    attendance: 0,
    monthsAttended: 24,
    isEmployed: true,
    employer: "Aurora Works",
    position: "Engineer",
    seniority: "Mid",
    salary: 210,
    isAtWork: true,
    performance: 0.86,
    warnings: 0,
    isLookingForWork: false,
    monthlyPension: null,
    retirementFund: null,
    pensionReduced: false,
    unemploymentCycle: null,
    isDead: false,
    householdNetworkId: 501,
    hasTraits: true,
    eyeColor: "Brown",
    hairColor: "Black",
    handedness: "Right",
    morphotype: "Athletic",
    aptitude: "Analytical",
    heightPotential: "Tall",
    sex: "Female",
    temperament: "Steady"
  },
  {
    networkId: 104,
    name: "Tomas Fenwick",
    age: 16,
    stage: "Education",
    ageTicks: 192,
    stageEnteredTick: 11000,
    lastTransitionCause: "Enrolled",
    health: 0.93,
    skillTotal: 12,
    topSkill: "Science",
    isEnrolled: true,
    isUnenrolled: false,
    schoolNetworkId: 805,
    school: "Market Academy",
    attendance: 0.92,
    monthsAttended: 60,
    isEmployed: false,
    employer: "",
    position: "",
    seniority: "",
    salary: 0,
    isAtWork: false,
    performance: 0,
    warnings: 0,
    isLookingForWork: false,
    monthlyPension: null,
    retirementFund: null,
    pensionReduced: false,
    unemploymentCycle: null,
    isDead: false,
    householdNetworkId: 502,
    hasTraits: true,
    eyeColor: "Green",
    hairColor: "Blonde",
    handedness: "Left",
    morphotype: "Slim",
    aptitude: "Creative",
    heightPotential: "Average",
    sex: "Male",
    temperament: "Curious"
  },
  {
    networkId: 103,
    name: "Petra Lindqvist",
    age: 68,
    stage: "Retirement",
    ageTicks: 816,
    stageEnteredTick: 9000,
    lastTransitionCause: "Retired",
    health: 0.72,
    skillTotal: 60,
    topSkill: "Medicine",
    isEnrolled: false,
    isUnenrolled: false,
    schoolNetworkId: null,
    school: "",
    attendance: 0,
    monthsAttended: 40,
    isEmployed: false,
    employer: "",
    position: "",
    seniority: "",
    salary: 0,
    isAtWork: false,
    performance: 0,
    warnings: 0,
    isLookingForWork: false,
    monthlyPension: 190,
    retirementFund: "Pension",
    pensionReduced: false,
    unemploymentCycle: null,
    isDead: false,
    householdNetworkId: 501,
    hasTraits: true,
    eyeColor: "Blue",
    hairColor: "Grey",
    handedness: "Right",
    morphotype: "Sturdy",
    aptitude: "Empathetic",
    heightPotential: "Average",
    sex: "Female",
    temperament: "Calm"
  }
];

// --- Players ----------------------------------------------------------------

export const playerOverview = {
  players: 3,
  active: 2,
  awaitingContinuation: 1,
  employed: 2,
  housed: 3,
  pendingInvitations: 1,
  deathsThisYear: 1,
  continuationsThisYear: 2,
  commandsAppliedThisYear: 47
};

export const playerLives = {
  items: [
    {
      playerId: "alice",
      genosNetworkId: 101,
      name: "Marceline Okonkwo",
      age: 34,
      health: 0.95,
      hunger: 0.2,
      energy: 0.7,
      hygiene: 0.9,
      bladder: 0.3,
      walletCents: 24050,
      savingsCents: 182000,
      isAlive: true,
      awaitingContinuation: false,
      continuationPolicy: "HeirThenObserver",
      observer: false,
      nutritionBalance: 12,
      consecutivePoorDays: 0,
      partnerGenosId: 102,
      partnerName: "Idris Bakker",
      children: 1,
      employment: {
        employed: true,
        companyId: 201,
        officeId: 301,
        position: "Engineer",
        seniority: "Mid",
        salaryCents: 21000,
        atWork: true,
        consecutiveMissedShifts: 0,
        lastAttendedTick: 12839
      },
      housing: {
        housed: true,
        householdId: 501,
        unitId: 601,
        propertyId: 801,
        propertyName: "Maple Court",
        unitNumber: "1A",
        tier: "Mid",
        rentCents: 12000,
        rentArrearsCents: 0,
        utilityArrearsCents: 0,
        leaseActive: true,
        foodUnits: 9,
        householdGoods: 4
      },
      jobOffer: null,
      leaseOffer: null,
      invitations: [],
      recentActions: [
        { commandId: UUID.a, kind: "Work", status: "Applied", code: null, tick: 12838 },
        { commandId: UUID.b, kind: "Eat", status: "Rejected", code: "NoFood", tick: 12830 }
      ]
    },
    {
      playerId: "bob",
      genosNetworkId: 105,
      name: "Selene Marchetti",
      age: 51,
      health: 0,
      hunger: 0,
      energy: 0,
      hygiene: 0,
      bladder: 0,
      walletCents: 0,
      savingsCents: 1200,
      isAlive: false,
      awaitingContinuation: true,
      continuationPolicy: "HeirThenObserver",
      observer: false,
      nutritionBalance: 0,
      consecutivePoorDays: 0,
      partnerGenosId: null,
      partnerName: null,
      children: 2,
      employment: {
        employed: false,
        companyId: 0,
        officeId: 0,
        position: "",
        seniority: "",
        salaryCents: 0,
        atWork: false,
        consecutiveMissedShifts: 0,
        lastAttendedTick: 0
      },
      housing: {
        housed: true,
        householdId: 502,
        unitId: 602,
        propertyId: 801,
        propertyName: "Maple Court",
        unitNumber: "1B",
        tier: "Low",
        rentCents: 8000,
        rentArrearsCents: 14000,
        utilityArrearsCents: 3000,
        leaseActive: false,
        foodUnits: 0,
        householdGoods: 0
      },
      jobOffer: null,
      leaseOffer: null,
      invitations: [
        {
          id: UUID.c,
          fromGenosId: 101,
          toGenosId: 105,
          kind: "Cohabitation",
          status: "Pending",
          expiresTick: 13000,
          createdTick: 12700
        }
      ],
      recentActions: []
    }
  ],
  total: 2
};

// --- Appearance -------------------------------------------------------------

export const appearancePage = {
  items: [
    {
      networkId: 101,
      name: "Marceline Okonkwo",
      age: 34,
      isPlayerControlled: true,
      playerId: "alice",
      stage: "Work",
      descriptor: appearanceDto,
      layers: appearanceLayers
    },
    {
      networkId: 104,
      name: "Tomas Fenwick",
      age: 16,
      isPlayerControlled: false,
      playerId: null,
      stage: "Education",
      descriptor: { ...appearanceDto, ageBand: 2, heightScale: 4 },
      layers: appearanceLayers
    }
  ],
  total: 2
};

// --- Consumption ------------------------------------------------------------

export const consumptionOverview = {
  networks: [
    {
      networkId: 1001,
      service: "Water",
      production: 1200,
      capacity: 1500,
      load: 900,
      reserve: 300,
      state: "Ok"
    },
    {
      networkId: 1002,
      service: "Electricity",
      production: 2200,
      capacity: 2400,
      load: 1900,
      reserve: 300,
      state: "Tight"
    }
  ],
  households: 14,
  cutOffHouseholds: 1,
  reducedHouseholds: 2,
  stores: 3,
  stockUnits: 540,
  spoiledThisYear: 12,
  nutrition: [
    { band: "Good", count: 10 },
    { band: "Fair", count: 3 },
    { band: "Poor", count: 1 }
  ],
  malnourishedHouseholds: 1,
  groceryCatalogVersion: 1
};

export const consumptionHouseholds = {
  items: [
    {
      householdId: 501,
      unitId: 601,
      occupants: 3,
      waterUsage: 42,
      electricityUsage: 88,
      waterLevel: "Good",
      electricityLevel: "Good",
      arrearsCents: 0,
      rentArrearsCents: 0,
      noUtilities: false,
      consecutivePoorMonths: 0
    },
    {
      householdId: 502,
      unitId: 602,
      occupants: 2,
      waterUsage: 12,
      electricityUsage: 20,
      waterLevel: "Poor",
      electricityLevel: "Poor",
      arrearsCents: 3000,
      rentArrearsCents: 14000,
      noUtilities: true,
      consecutivePoorMonths: 2
    }
  ],
  total: 2
};

export const consumptionStores = {
  items: [
    {
      storeNetworkId: 802,
      name: "Corner Grocer",
      items: [
        { itemIndex: 0, itemId: "bread", stock: 40, priceCents: 250, lastRestockTick: 12800 },
        { itemIndex: 1, itemId: "milk", stock: 22, priceCents: 180, lastRestockTick: 12790 },
        { itemIndex: 2, itemId: "apples", stock: 60, priceCents: 120, lastRestockTick: 12810 }
      ]
    }
  ],
  total: 1
};

// --- Social -----------------------------------------------------------------

export const socialOverview = {
  tick: 12840,
  online: [
    {
      playerId: "alice",
      displayName: "Alice",
      genosNetworkId: 101,
      tileX: 10,
      tileY: 10,
      buildingNetworkId: 802,
      state: "InVenue",
      lastSeenTick: 12840
    }
  ],
  venues: [
    {
      venueNetworkId: 802,
      name: "Central Cafe",
      kind: "Cafe",
      occupancy: 1,
      capacity: 8,
      isOpen: true,
      isClosed: false
    }
  ],
  reports: [
    {
      id: UUID.a,
      reporterPlayerId: "bob",
      targetPlayerId: "alice",
      reason: "Harassment",
      messageId: null,
      evidence: "evidenced message",
      createdTick: 12820,
      status: "Open"
    }
  ],
  edges: [
    { fromPlayerId: "alice", toPlayerId: "bob", kind: "Friend", sinceTick: 1, source: "Request" }
  ],
  requests: [
    {
      id: UUID.b,
      fromPlayerId: "carol",
      toPlayerId: "alice",
      kind: "Friend",
      status: "Pending",
      createdTick: 12800,
      expiresTick: 12900
    }
  ],
  partnershipInvitations: [
    {
      id: UUID.c,
      fromPlayerId: "alice",
      toPlayerId: "bob",
      kind: "Cohabitation",
      status: "Pending",
      createdTick: 12810,
      expiresTick: 12910
    }
  ],
  moderationActions: [
    {
      id: "44444444-4444-4444-4444-444444444444",
      reportId: UUID.a,
      reversesActionId: null,
      moderatorId: "admin-1",
      targetPlayerId: "alice",
      counterpartyPlayerId: "bob",
      kind: "Warn",
      durationTicks: 0,
      createdTick: 12830,
      endsTick: null,
      reason: "first warning",
      evidence: "evidenced message",
      messageId: null,
      venueNetworkId: null
    }
  ],
  privacy: [
    { playerId: "alice", showDisplayName: true, allowNearbyForNonFriends: false, friendsOnlyMessages: true }
  ],
  chatRetention: {
    retainedMessages: 12,
    deletedMessages: 1,
    oldestRetained: "2026-01-01T00:00:00+00:00",
    checkedAt: "2026-01-02T00:00:00+00:00",
    retentionDays: 30
  }
};
