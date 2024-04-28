CREATE TABLE public.positions
(
    "Id" bigint NOT NULL,
    "Entity" uuid NOT NULL,
    "X" double precision NOT NULL,
    "Y" double precision NOT NULL,
    "Z" double precision NOT NULL,
    PRIMARY KEY ("Id")
);

ALTER TABLE IF EXISTS public.positions
    OWNER to my_data_wh_user;