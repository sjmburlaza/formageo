export interface Project {
  id: string;
  name: string;
  createdAtUtc: string;
  siteCount: number;
}

export interface CreateProjectRequest {
  name: string;
}
