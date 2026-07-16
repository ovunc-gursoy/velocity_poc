CREATE TABLE tournaments (
    year        INTEGER PRIMARY KEY,
    hosts       TEXT    NOT NULL,
    winner      TEXT,           -- NULL until the final has been played
    runner_up   TEXT,
    score       TEXT,
    venue       TEXT,
    city        TEXT,
    attendance  INTEGER,
    notes       TEXT
);

INSERT INTO tournaments (year, hosts, winner, runner_up, score, venue, city, attendance, notes) VALUES
(1930, 'Uruguay',              'Uruguay',      'Argentina',      '4-2',            'Estadio Centenario',           'Montevideo',     68346,  NULL),
(1934, 'Italy',                'Italy',        'Czechoslovakia', '2-1 (a.e.t.)',   'Stadio Nazionale PNF',         'Rome',           55000,  NULL),
(1938, 'France',               'Italy',        'Hungary',        '4-2',            'Stade Olympique de Colombes',  'Paris',          45000,  NULL),
(1950, 'Brazil',               'Uruguay',      'Brazil',         '2-1',            'Maracana',                     'Rio de Janeiro', 173850, 'Decided by a final-round group match, not a one-off final. Known as the Maracanazo.'),
(1954, 'Switzerland',          'West Germany', 'Hungary',        '3-2',            'Wankdorf Stadium',             'Bern',           62500,  'The Miracle of Bern.'),
(1958, 'Sweden',               'Brazil',       'Sweden',         '5-2',            'Rasunda Stadium',              'Solna',          49737,  'Pele, aged 17, scored twice.'),
(1962, 'Chile',                'Brazil',       'Czechoslovakia', '3-1',            'Estadio Nacional',             'Santiago',       68679,  NULL),
(1966, 'England',              'England',      'West Germany',   '4-2 (a.e.t.)',   'Wembley Stadium',              'London',         96924,  'Geoff Hurst hat-trick; the disputed third goal.'),
(1970, 'Mexico',               'Brazil',       'Italy',          '4-1',            'Estadio Azteca',               'Mexico City',    107412, 'Brazil won the Jules Rimet trophy outright with a third title.'),
(1974, 'West Germany',         'West Germany', 'Netherlands',    '2-1',            'Olympiastadion',               'Munich',         75200,  NULL),
(1978, 'Argentina',            'Argentina',    'Netherlands',    '3-1 (a.e.t.)',   'Estadio Monumental',           'Buenos Aires',   71483,  NULL),
(1982, 'Spain',                'Italy',        'West Germany',   '3-1',            'Santiago Bernabeu',            'Madrid',         90000,  NULL),
(1986, 'Mexico',               'Argentina',    'West Germany',   '3-2',            'Estadio Azteca',               'Mexico City',    114600, 'Maradona''s tournament.'),
(1990, 'Italy',                'West Germany', 'Argentina',      '1-0',            'Stadio Olimpico',              'Rome',           73603,  NULL),
(1994, 'United States',        'Brazil',       'Italy',          '0-0 (3-2 pens)', 'Rose Bowl',                    'Pasadena',       94194,  'First final decided on penalties.'),
(1998, 'France',               'France',       'Brazil',         '3-0',            'Stade de France',              'Saint-Denis',    80000,  NULL),
(2002, 'South Korea / Japan',  'Brazil',       'Germany',        '2-0',            'International Stadium',        'Yokohama',       69029,  'First tournament hosted by two nations.'),
(2006, 'Germany',              'Italy',        'France',         '1-1 (5-3 pens)', 'Olympiastadion',               'Berlin',         69000,  'Zidane sent off for headbutting Materazzi.'),
(2010, 'South Africa',         'Spain',        'Netherlands',    '1-0 (a.e.t.)',   'Soccer City',                  'Johannesburg',   84490,  'First tournament held in Africa.'),
(2014, 'Brazil',               'Germany',      'Argentina',      '1-0 (a.e.t.)',   'Maracana',                     'Rio de Janeiro', 74738,  'Germany beat Brazil 7-1 in the semi-final.'),
(2018, 'Russia',               'France',       'Croatia',        '4-2',            'Luzhniki Stadium',             'Moscow',         78011,  NULL),
(2022, 'Qatar',                'Argentina',    'France',         '3-3 (4-2 pens)', 'Lusail Stadium',               'Lusail',         88966,  'Messi''s first World Cup title.'),
(2026, 'United States / Canada / Mexico', NULL, NULL, NULL,                        'MetLife Stadium',              'East Rutherford', NULL,  'Not yet played. First 48-team tournament; final scheduled for 19 July 2026.');
