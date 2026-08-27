begin;

create table if not exists public.study_prompts
(
    id uuid primary key,
    prompt_text text not null check (length(btrim(prompt_text)) between 1 and 280),
    is_enabled boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.app_config
(
    id text primary key check (id = 'default'),
    active_prompt_id uuid not null references public.study_prompts(id)
);

create table if not exists public.journal_records
(
    id uuid primary key,
    owner_id uuid not null references auth.users(id) on delete cascade,
    created_utc timestamptz not null,
    uploaded_utc timestamptz not null default now(),
    text text not null check (length(btrim(text)) > 0),
    image_path text not null,
    image_width integer not null check (image_width > 0),
    image_height integer not null check (image_height > 0),
    prompt_id uuid null references public.study_prompts(id),
    prompt_text text null,
    client_schema_version integer not null check (client_schema_version > 0),
    constraint journal_records_owned_image_path
        check (image_path = owner_id::text || '/' || id::text || '.jpg')
);

alter table public.study_prompts enable row level security;
alter table public.app_config enable row level security;
alter table public.journal_records enable row level security;

revoke all on public.study_prompts from anon;
revoke all on public.app_config from anon;
revoke all on public.journal_records from anon;
revoke all on public.study_prompts from authenticated;
revoke all on public.app_config from authenticated;
revoke all on public.journal_records from authenticated;

grant select on public.study_prompts to authenticated;
grant select on public.app_config to authenticated;
grant select, insert, update on public.journal_records to authenticated;

drop policy if exists study_prompts_read_authenticated on public.study_prompts;
create policy study_prompts_read_authenticated
on public.study_prompts
for select
to authenticated
using (true);

drop policy if exists app_config_read_authenticated on public.app_config;
create policy app_config_read_authenticated
on public.app_config
for select
to authenticated
using (true);

drop policy if exists journal_records_read_own on public.journal_records;
create policy journal_records_read_own
on public.journal_records
for select
to authenticated
using ((select auth.uid()) = owner_id);

drop policy if exists journal_records_insert_own on public.journal_records;
create policy journal_records_insert_own
on public.journal_records
for insert
to authenticated
with check ((select auth.uid()) = owner_id);

drop policy if exists journal_records_update_own on public.journal_records;
create policy journal_records_update_own
on public.journal_records
for update
to authenticated
using ((select auth.uid()) = owner_id)
with check ((select auth.uid()) = owner_id);

insert into storage.buckets
(
    id,
    name,
    public,
    file_size_limit,
    allowed_mime_types
)
values
(
    'journal-images',
    'journal-images',
    false,
    6291456,
    array['image/jpeg']::text[]
)
on conflict (id) do update
set public = excluded.public,
    file_size_limit = excluded.file_size_limit,
    allowed_mime_types = excluded.allowed_mime_types;

drop policy if exists journal_images_insert_own on storage.objects;
create policy journal_images_insert_own
on storage.objects
for insert
to authenticated
with check
(
    bucket_id = 'journal-images'
    and (storage.foldername(name))[1] = (select auth.uid()::text)
);

drop policy if exists journal_images_read_own on storage.objects;
create policy journal_images_read_own
on storage.objects
for select
to authenticated
using
(
    bucket_id = 'journal-images'
    and owner_id = (select auth.uid()::text)
    and (storage.foldername(name))[1] = (select auth.uid()::text)
);

drop policy if exists journal_images_update_own on storage.objects;
create policy journal_images_update_own
on storage.objects
for update
to authenticated
using
(
    bucket_id = 'journal-images'
    and owner_id = (select auth.uid()::text)
    and (storage.foldername(name))[1] = (select auth.uid()::text)
)
with check
(
    bucket_id = 'journal-images'
    and owner_id = (select auth.uid()::text)
    and (storage.foldername(name))[1] = (select auth.uid()::text)
);

create or replace view public.journal_records_csv
with (security_invoker = true)
as
select
    id as record_id,
    created_utc,
    uploaded_utc,
    case
        when text ~ '^[[:space:]]*[=+@-]' then '''' || text
        else text
    end as entry_text,
    format('%sx%s JPEG at %s', image_width, image_height, image_path) as image_metadata,
    case
        when prompt_id is null then format('fallback: %s', coalesce(prompt_text, 'Free reflection'))
        else format('%s: %s', prompt_id, coalesce(prompt_text, ''))
    end as prompt_metadata,
    client_schema_version
from public.journal_records;

revoke all on public.journal_records_csv from anon;
grant select on public.journal_records_csv to authenticated;

insert into public.study_prompts (id, prompt_text, is_enabled)
values
    ('11111111-1111-4111-8111-111111111111', 'What felt meaningful today?', true),
    ('22222222-2222-4222-8222-222222222222', 'What would you like to remember tomorrow?', true)
on conflict (id) do update
set prompt_text = excluded.prompt_text,
    is_enabled = excluded.is_enabled,
    updated_at = now();

insert into public.app_config (id, active_prompt_id)
values ('default', '11111111-1111-4111-8111-111111111111')
on conflict (id) do nothing;

commit;
